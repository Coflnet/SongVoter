using System;
using Microsoft.AspNetCore.Mvc;
using Coflnet.SongVoter.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Coflnet.SongVoter.Controllers.Impl
{
    public class AuthApiControllerImpl : AuthApiController
    {
        public override IActionResult AuthWithGoogle([FromBody] AuthToken authToken)
        {
            string key = SimplerConfig.Config.Instance["jwt:secret"]; //Secret key which will be used later during validation    
            var issuer = "http://mysite.com"; //normally this will be your site URL    

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            //Create a List of Claims, Keep claims name short    
            var permClaims = new List<Claim>();
            permClaims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            permClaims.Add(new Claim("valid", "1"));
            permClaims.Add(new Claim("userid", "1"));
            permClaims.Add(new Claim("scopes", "[read:song]"));
           // var m = new SignInManager<object>();
           //  var principal = await .CreateUserPrincipalAsync();

            //Create Security Token object by giving required parameters    
            var token = new JwtSecurityToken(issuer, //Issure    
                issuer, //Audience    
                permClaims,
                expires : DateTime.Now.AddDays(1),
                signingCredentials : credentials);
            var jwt_token = new JwtSecurityTokenHandler().WriteToken(token);
            return Ok(new { token = jwt_token });
        }
    }
}
