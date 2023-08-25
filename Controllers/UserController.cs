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
    [Route("/user/name")]
    [ValidateModelState]
    [SwaggerOperation("UpdateUserName")]
    [SwaggerResponse(statusCode: 200, type: typeof(User), description: "successful updated")]
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
    [Route("/user/spotify/token")]
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
        if (token.Expiration < DateTime.UtcNow + TimeSpan.FromMinutes(5))
        {
            if(token.RefreshToken == null)
            {
                throw new Core.ApiException("no_spotify_token", "No spotify refresh token found");
            }
            var newToken = await new OAuthClient().RequestToken(
                new AuthorizationCodeRefreshRequest(
                                config["spotify:clientid"],
                                config["spotify:clientsecret"],
                                token.RefreshToken)
            );

            token.AccessToken = newToken.AccessToken;
            token.Expiration = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
            token.RefreshToken = newToken.RefreshToken;
            db.Update(token);
            await db.SaveChangesAsync();
        }

        return token;
    }

    [HttpGet]
    [Route("/user/info")]
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
    [Route("/user/spotify")]
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
}