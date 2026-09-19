using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Coflnet.SongVoter.DBModels;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Coflnet.SongVoter.Service;

public class GuestAuthentication(IConfiguration configuration, IDService ids)
{
    public const string Issuer = "https://songvoter.party";

    public static string IdentityHash(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public static bool Verify(AuthChallenge challenge, string secret, long counter, DateTime now)
    {
        if (counter < 0 || challenge.ExpiresAt <= now || IdentityHash(secret) != challenge.IdentityHash)
            return false;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{challenge.Id}:{secret}:{counter.ToString(CultureInfo.InvariantCulture)}"));
        for (var bit = 0; bit < challenge.Difficulty; bit++)
            if ((hash[bit / 8] & (128 >> (bit % 8))) != 0)
                return false;
        return true;
    }

    public object Session(User user)
    {
        var expires = DateTime.UtcNow.AddDays(1);
        var token = new JwtSecurityToken(Issuer, Issuer,
            [new Claim("uid", ids.ToHash(user.Id))], expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["jwt:secret"])), SecurityAlgorithms.HmacSha256));
        return new { token = new JwtSecurityTokenHandler().WriteToken(token), expiresAt = expires,
            userId = ids.ToHash(user.Id), name = user.Name, anonymous = user.IsAnonymous };
    }
}
