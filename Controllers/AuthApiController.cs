using System;
using Microsoft.AspNetCore.Mvc;
using Coflnet.SongVoter.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Coflnet.SongVoter.DBModels;
using Google.Apis.Auth;
using Coflnet.SongVoter.Middleware;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;
using Newtonsoft.Json;
using Coflnet.SongVoter.Attributes;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Services;

namespace Coflnet.SongVoter.Controllers
{
    public class AuthApiControllerImpl : ControllerBase
    {
        private readonly SVContext db;
        private readonly IConfiguration config;
        private readonly IDService idService;
        public AuthApiControllerImpl(SVContext data, IConfiguration config, IDService idService)
        {
            this.db = data;
            this.config = config;
            this.idService = idService;
            Console.WriteLine($"Token for root user {CreateTokenFor(0)}");
        }

        /// <summary>
        /// Authenticate with google
        /// </summary>
        /// <remarks>Exchange a google identity token for a songvoter token</remarks>
        /// <param name="authToken">The google identity token</param>
        /// <response code="200">successful operation</response>
       /* [HttpPost]
        [Route("/auth/google")]
        [Consumes("application/json")]
        [ValidateModelState]
        [SwaggerOperation("AuthWithGoogle")]
        [SwaggerResponse(statusCode: 200, type: typeof(AuthToken), description: "successful operation")]
        public async Task<IActionResult> AuthWithGoogle([FromBody] AuthToken authToken)
        {
            var data = ValidateToken(authToken.Token);
            return await GetTokenForUser(data);
        }*/


        public class AuthCode
        {
            public string Code { get; set; }
            public string RedirectUri { get; set; }
        }

        /// <summary>
        /// Stores google auth token server side
        /// </summary>
        /// <param name="refreshToken">The google refresh token</param>
        /// <response code="200">successful operation</response>
        [HttpPost]
        [Route("/auth/google")]
        [Consumes("application/json")]
        [ValidateModelState]
        [SwaggerOperation("AuthWithGoogleCode")]
        [SwaggerResponse(statusCode: 200, type: typeof(AuthToken), description: "successful operation")]
        public async Task<AuthToken> AuthWithGoogleCode([FromBody] AuthRefreshToken refreshToken)
        {
            // store refresh token
            var data = ValidateToken(refreshToken.Token);
            _ = Task.Run(async () =>
            {
                try
                {
                    Google.Apis.YouTube.v3.YouTubeService yt = new Google.Apis.YouTube.v3.YouTubeService(
                        new BaseClientService.Initializer()
                        {
                            ApiKey = refreshToken.AccessToken
                        });

                    var request = yt.Search.List("snippet");
                    request.Q = "pokerface";
                    request.MaxResults = 20;
                    request.Type = "video";

                    var response = await request.ExecuteAsync();
                    Console.WriteLine(JsonConvert.SerializeObject(response));
                }
                catch (System.Exception)
                {
                    Console.WriteLine("Failed to get youtube data with login token");
                }
            });
            return await GetTokenForUser(data, refreshToken.RefreshToken, refreshToken.AccessToken);
        }

        [HttpPost]
        [Route("/auth/spotify/code")]
        [Consumes("application/json")]
        [ValidateModelState]
        [SwaggerOperation("AuthWithSpotify")]
        [SwaggerResponse(statusCode: 200, type: typeof(AuthToken), description: "successful operation")]
        public async Task<IActionResult> AuthWithSpotify([FromBody] AuthCode authCode)
        {
            try
            {
                var uri = new Uri(authCode.RedirectUri ?? "com.coflnet.songvoter://account");
                Console.WriteLine("Auth with spotify code " + authCode.Code + " redirect " + uri);
                var token = await new OAuthClient().RequestToken(new AuthorizationCodeTokenRequest(
                                config["spotify:clientid"],
                                config["spotify:clientsecret"],
                                authCode.Code,
                                uri
                            ));
                var spotify = new SpotifyClient(token.AccessToken);
                var me = await spotify.UserProfile.Current();
                var userId = db.Users
                    .Where(u => u.Tokens.Where(t => t.ExternalId == me.Id && t.Platform == Platforms.Spotify).Any())
                    .Select(u => u.Id).FirstOrDefault();
                if (userId == 0)
                {
                    var user = new User()
                    {
                        Name = me.DisplayName,
                        Tokens = new List<Oauth2Token>() { new Oauth2Token() {
                        ExternalId = me.Id,
                        Platform = Platforms.Spotify,
                        // add refresh token
                        AccessToken = token.AccessToken,
                    } }
                    };
                    db.Add(user);
                    await db.SaveChangesAsync();
                    userId = user.Id;
                }
                return Ok(new { token = CreateTokenFor(userId) });
            }
            catch (SpotifyAPI.Web.APIException e)
            {
                Console.WriteLine(JsonConvert.SerializeObject(e.Response));
                return this.Problem(e.Message);
            }

        }

        private async Task<AuthToken> GetTokenForUser(GoogleJsonWebSignature.Payload data, string refreshToken = null, string accessToken = null)
        {
            var userId = db.Users.Where(u => u.GoogleId == data.Subject).Select(u => u.Id).FirstOrDefault();
            if (userId == 0)
            {
                var user = new User()
                {
                    GoogleId = data.Subject,
                    Name = data.Name,
                    Tokens = new List<Oauth2Token>() { new Oauth2Token() {
                        ExternalId = data.Subject,
                        Platform = Platforms.Youtube,
                        // add refresh token
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        Expiration = DateTime.UtcNow.AddSeconds(data.ExpirationTimeSeconds.Value)
                    } }
                };
                db.Add(user);
                await db.SaveChangesAsync();
                userId = user.Id;
            }

            return new AuthToken() { Token = CreateTokenFor(userId) };
        }


        [HttpPost]
        [Route("/auth/test")]
        [Consumes("application/json")]
        public async Task<IActionResult> AuthWithTestToken([FromBody] AuthToken token)
        {
            var savedToken = config["test:authtoken"];
            Console.WriteLine("Creating token for test user " + savedToken);
            if (string.IsNullOrEmpty(savedToken))
                return this.Problem("test mode not active, please set test:authtoken");

            if (savedToken != token.Token)
                return this.Problem("invalid token passed");


            var payload = new GoogleJsonWebSignature.Payload()
            {
                Subject = "2",
                Name = "testUser"
            };

            return Ok(await GetTokenForUser(payload));
        }

        [HttpDelete]
        [Route("/db")]
        [Consumes("application/json")]
        public async Task<IActionResult> Drop([FromBody] AuthToken token)
        {
            var savedToken = config["db:authtoken"];
            Console.WriteLine("Attempt to drop db");
            if (string.IsNullOrEmpty(savedToken))
                return this.Problem("please set db:authtoken");

            if (savedToken != token.Token)
                return this.Problem("invalid token passed");

            db.Database.EnsureDeleted();

            return Ok("dropped I hope you are not evil");
        }

        [HttpPost]
        [Route("/db")]
        [Consumes("application/json")]
        public async Task<IActionResult> MigrateDb([FromBody] AuthToken token)
        {
            var savedToken = config["db:authtoken"];
            if (string.IsNullOrEmpty(savedToken))
                return this.Problem("please set db:authtoken");

            if (savedToken != token.Token)
                return this.Problem("invalid token passed");

            await db.Database.MigrateAsync();

            return Ok("migrated");
        }

        private string CreateTokenFor(int userId)
        {
            string key = config["jwt:secret"]; //Secret key which will be used later during validation    
            var issuer = "http://mysite.com"; //normally this will be your site URL    

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            //Create a List of Claims, Keep claims name short    
            var permClaims = new List<Claim>();
            permClaims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            // userLevel
            permClaims.Add(new Claim("ul", "1"));
            permClaims.Add(new Claim("uid", idService.ToHash(userId)));

            //Create Security Token object by giving required parameters    
            var token = new JwtSecurityToken(issuer, //Issure    
                issuer, //Audience    
                permClaims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: credentials);
            var jwt_token = new JwtSecurityTokenHandler().WriteToken(token);
            return jwt_token;
        }

        public static GoogleJsonWebSignature.Payload ValidateToken(string token)
        {
            try
            {
                var client = GoogleJsonWebSignature.ValidateAsync(token);
                client.Wait();
                var tokenData = client.Result;
                Console.WriteLine("google user: " + tokenData.Name);
                return tokenData;
            }
            catch (Exception e)
            {
                throw new ApiException(System.Net.HttpStatusCode.InternalServerError, $"{e.InnerException.Message}");
            }
        }
    }
}
