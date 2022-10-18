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

namespace Coflnet.SongVoter.Controllers.Impl
{
    public class AuthApiControllerImpl : AuthApiController
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

        public override async Task<IActionResult> AuthWithGoogle([FromBody] AuthToken authToken)
        {
            var data = ValidateToken(authToken.Token);
            return await GetTokenForUser(data);
        }

        private async Task<IActionResult> GetTokenForUser(GoogleJsonWebSignature.Payload data)
        {
            var userId = db.Users.Where(u => u.GoogleId == data.Subject).Select(u => u.Id).FirstOrDefault();
            if (userId == 0)
            {
                var user = new User() { GoogleId = data.Subject, Name = data.Name };
                db.Add(user);
                await db.SaveChangesAsync();
                userId = user.Id;
            }

            return Ok(new { token = CreateTokenFor(userId) });
        }


        [HttpPost]
        [Route("/v1/auth/test")]
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

            return await GetTokenForUser(payload);
        }

        [HttpDelete]
        [Route("/v1/db")]
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
        [Route("/v1/db")]
        [Consumes("application/json")]
        public async Task<IActionResult> MigrateDb([FromBody] AuthToken token)
        {
            var savedToken = config["db:authtoken"];
            if (string.IsNullOrEmpty(savedToken))
                return this.Problem("please set db:authtoken");

            if (savedToken != token.Token)
                return this.Problem("invalid token passed");

            db.Database.Migrate();

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
