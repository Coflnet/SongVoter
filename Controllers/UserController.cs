using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.Attributes;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Models;
using Coflnet.SongVoter.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;
using Swashbuckle.AspNetCore.Annotations;

namespace Coflnet.SongVoter.Controllers;
[Route("api/user")]
public class UserController : ControllerBase
{
    private readonly SVContext db;
    private IDService idService;
    private IConfiguration config;
    private ILogger<UserController> logger;
    public UserController(SVContext data, IDService idService, IConfiguration config, ILogger<UserController> logger)
    {
        this.db = data;
        this.idService = idService;
        this.config = config;
        this.logger = logger;
    }
    /// <summary>
    /// Updates the display name of the current user
    /// </summary>
    /// <response code="200">successful created</response>
    [HttpPost]
    [Route("name")]
    [ValidateModelState]
    [SwaggerOperation("UpdateUserName")]
    [SwaggerResponse(statusCode: 200, description: "successful updated")]
    public async Task<IActionResult> UpdateName([FromBody, Required] string name)
    {
        var user = await db.Users.FindAsync((int)idService.UserId(this));
        user.Name = name;
        db.Update(user);
        await db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// Returns spotify access token to control music playback client side
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("spotify/token")]
    [Consumes("application/json")]
    public async Task<ActionResult<string>> GetSpotifyToken()
    {
        var user = await db.Users.Where(u => u.Id == (int)idService.UserId(this)).Include(u => u.Tokens.Where(t => t.Platform == Platforms.Spotify)).FirstOrDefaultAsync();
        if (user == null)
        {
            return NotFound();
        }
        Oauth2Token token = await GetUpToDateToken(user);
        if (token == null)
        {
            throw new Core.ApiException("no_spotify_token", "No spotify token found");
        }
        return token.AccessToken;
    }

    private async Task<Oauth2Token> GetUpToDateToken(User user)
    {
        var token = user.Tokens.FirstOrDefault(t => t.Platform == Platforms.Spotify);
        if (token == null)
        {
            return null;
        }
        logger.LogInformation("Spotify token expires at {0} refresh token starts with {1}", token.Expiration, token.RefreshToken?.Substring(0, 5));
        // refresh token if needed
        if (token.Expiration < DateTime.UtcNow + TimeSpan.FromMinutes(5) && token.AccessToken != null)
        {
            if(token.RefreshToken == null)
            {
                token.AccessToken = null;
                db.Update(token);
                await db.SaveChangesAsync();
                return null;
            }
            var newToken = await new OAuthClient().RequestToken(
                new AuthorizationCodeRefreshRequest(
                                config["spotify:clientid"],
                                config["spotify:clientsecret"],
                                token.RefreshToken)
            );

            token.AccessToken = newToken.AccessToken;
            token.Expiration = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
            token.RefreshToken = newToken.RefreshToken ?? token.RefreshToken;
            logger.LogInformation($"New token info: {token.AccessToken} expires at {token.Expiration} refresh token starts with {token.RefreshToken?.Substring(0, 5)}");
            db.Update(token);
            await db.SaveChangesAsync();
        }

        return token;
    }

    [HttpGet]
    [Route("info")]
    public async Task<UserInfo> GetUserInfo()
    {
        var user = await db.Users.Where(u => u.Id == (int)idService.UserId(this)).Include(u => u.Tokens.Where(t => t.Platform == Platforms.Spotify)).FirstOrDefaultAsync();
        Oauth2Token token = await GetUpToDateToken(user);
        return new UserInfo()
        {
            UserId = idService.ToHash(user.Id),
            UserName = user.Name,
            SpotifyToken = token?.AccessToken,
            SpotifyTokenExpiration = token?.Expiration
        };
    }

    /// <summary>
    /// Disconnects spotify from the current user
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Core.ApiException"></exception>
    [HttpDelete]
    [Route("spotify")]
    public async Task<UserInfo> DisconnectSpotify()
    {
        var user = await db.Users.Where(u => u.Id == (int)idService.UserId(this)).Include(u => u.Tokens.Where(t => t.Platform == Platforms.Spotify)).FirstOrDefaultAsync();
        var token = user.Tokens.FirstOrDefault(t => t.Platform == Platforms.Spotify);
        if (token == null)
        {
            throw new Core.ApiException("no_spotify_token", "No spotify token found");
        }
        db.Remove(token);
        await db.SaveChangesAsync();
        return await GetUserInfo();
    }

    /// <summary>
    /// Deletes the current user, this action is final and cannot be undone
    /// </summary>
    /// <returns></returns>
    [HttpDelete]
    [Route("")]
    [SwaggerOperation("DeleteUser")]
    [SwaggerResponse(statusCode: 200, description: "successful deleted")]
    public async Task<IActionResult> DeleteUser()
    {
        // delete owned parties
        var user = await db.Users.Where(u => u.Id == (int)idService.UserId(this)).Include(u => u.Tokens).Include(u => u.Upvotes).Include(u => u.Downvotes).FirstOrDefaultAsync();
        var parties = await db.Parties.Where(p => p.Creator == user).ToListAsync();
        db.RemoveRange(parties);
        db.Remove(user);
        await db.SaveChangesAsync();
        return Ok();
    }
}