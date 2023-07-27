using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.Attributes;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Coflnet.SongVoter.Controllers;
public class UserController : ControllerBase
{
    private readonly SVContext db;
    private IDService idService;
    public UserController(SVContext data, IDService idService)
    {
        this.db = data;
        this.idService = idService;
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
        var token = user.Tokens.FirstOrDefault(t => t.Platform == Platforms.Spotify);
        if (token == null)
        {
            return NotFound();
        }
        return token.AccessToken;
    }
}