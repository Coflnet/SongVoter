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

namespace Coflnet.SongVoter.Controllers.Impl
{
    public class AuthApiControllerImpl : AuthApiController
    {
        private readonly SVContext db;
        public AuthApiControllerImpl(SVContext data)
        {
            this.db = data;
        }

        public override IActionResult AuthWithGoogle([FromBody] AuthToken authToken)
        {
            var data = ValidateToken(authToken.Token);
            var userId = db.Users.Where(u => u.GoogleId == data.Subject).Select(u => u.Id).FirstOrDefault();
            if (userId == 0)
            {
                var user = new User() { GoogleId = data.Subject, Name = data.Name };
                db.Add(user);
                db.SaveChanges ();
                userId = user.Id;
            }

            return CreateTokenFor(userId);
        }

        private IActionResult CreateTokenFor(int userId)
        {
            string key = SimplerConfig.Config.Instance["jwt:secret"]; //Secret key which will be used later during validation    
            var issuer = "http://mysite.com"; //normally this will be your site URL    

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            //Create a List of Claims, Keep claims name short    
            var permClaims = new List<Claim>();
            permClaims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            // userLevel
            permClaims.Add(new Claim("ul", "1"));
            permClaims.Add(new Claim("uid", userId.ToString()));

            //Create Security Token object by giving required parameters    
            var token = new JwtSecurityToken(issuer, //Issure    
                issuer, //Audience    
                permClaims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: credentials);
            var jwt_token = new JwtSecurityTokenHandler().WriteToken(token);
            return Ok(new { token = jwt_token });
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
            } catch(Exception e)
            {
                throw new ApiException(System.Net.HttpStatusCode.InternalServerError,$"{e.InnerException.Message}");
            }

        }
    }
}
