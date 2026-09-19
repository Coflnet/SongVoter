using System.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Google.Apis.Auth;

namespace Coflnet.SongVoter.Controllers;

[ApiController, Route("api/auth")]
public class AuthApiController(SVContext db, GuestAuthentication auth, IDService ids, IConfiguration config) : ControllerBase
{
    public record ChallengeRequest([Required, RegularExpression("^[a-f0-9]{64}$")] string IdentityHash);
    public record ProofRequest([Required, StringLength(64, MinimumLength = 64)] string ChallengeId,
        [Required, RegularExpression("^[a-f0-9]{64}$")] string Secret, [Range(0, long.MaxValue)] long Counter);

    [HttpPost("challenge"), AllowAnonymous, EnableRateLimiting("authentication")]
    public async Task<IActionResult> Challenge(ChallengeRequest request)
    {
        await db.AuthChallenges.Where(c => c.ExpiresAt <= DateTime.UtcNow).ExecuteDeleteAsync();
        var challenge = new AuthChallenge {
            Id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), IdentityHash = request.IdentityHash,
            Difficulty = Math.Clamp(config.GetValue("Authentication:Difficulty", 20), 16, 24),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        db.Add(challenge);
        await db.SaveChangesAsync();
        return Ok(new { challenge.Id, challenge.Difficulty, challenge.ExpiresAt });
    }

    [HttpPost("anonymous"), AllowAnonymous, EnableRateLimiting("authentication")]
    public async Task<IActionResult> Anonymous(ProofRequest request)
    {
        var challenge = await db.AuthChallenges.FindAsync(request.ChallengeId);
        if (challenge == null || !GuestAuthentication.Verify(challenge, request.Secret, request.Counter, DateTime.UtcNow))
            return Unauthorized("Please retry connecting to the party.");
        // Database consumption makes a proof single-use across replicas and restarts.
        using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.AuthChallenges.Where(c => c.Id == challenge.Id && c.ExpiresAt > DateTime.UtcNow).ExecuteDeleteAsync() != 1)
            return Unauthorized("This challenge has already been used.");
        var user = await db.Users.SingleOrDefaultAsync(u => u.DeviceKeyHash == challenge.IdentityHash);
        if (user == null)
        {
            user = new User { DeviceKeyHash = challenge.IdentityHash, Name = "Guest" };
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Response.Headers.CacheControl = "no-store";
        return Ok(auth.Session(user));
    }

    // Optional account upgrade keeps the same profile, favourites, and event ownership.
    [HttpPost("google"), Authorize]
    public async Task<IActionResult> Google(Models.AuthToken request)
    {
        if (string.IsNullOrWhiteSpace(config["google:clientid"]))
            return StatusCode(503, "Google account linking is not configured.");
        GoogleJsonWebSignature.Payload payload;
        try {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.Token,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [config["google:clientid"]] });
        } catch (InvalidJwtException) { return Unauthorized("Invalid Google identity token."); }
        var user = await db.Users.FindAsync(ids.UserId(this));
        if (await db.Users.AnyAsync(u => u.GoogleId == payload.Subject && u.Id != user.Id))
            return Conflict("This Google account is already linked to another profile.");
        user.GoogleId = payload.Subject;
        user.Name = payload.Name ?? user.Name;
        await db.SaveChangesAsync();
        return Ok(auth.Session(user));
    }
}
