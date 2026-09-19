using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Coflnet.SongVoter.Controllers;

[ApiController, Authorize, Route("api/user")]
public class UserController(SVContext db, IDService ids, PartyService parties) : ControllerBase
{
    [HttpGet("info")]
    public async Task<IActionResult> Get()
    {
        var user = await db.Users.FindAsync(ids.UserId(this));
        return user == null ? Unauthorized() : Ok(new { userId = ids.ToHash(user.Id), userName = user.Name, anonymous = user.IsAnonymous });
    }

    [HttpPost("name")]
    public async Task<IActionResult> Rename([FromBody, Required, StringLength(40, MinimumLength = 1)] string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Enter a name.");
        var user = await db.Users.FindAsync(ids.UserId(this));
        user.Name = name.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        var user = await db.Users.FindAsync(ids.UserId(this));
        if (await parties.GetUserParty(user, true) != null) await parties.LeaveParty(user);
        db.PlayLists.RemoveRange(await db.PlayLists.Where(p => p.Owner == user.Id).ToListAsync());
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
