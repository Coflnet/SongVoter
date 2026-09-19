using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Coflnet.SongVoter.Transformers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.SongVoter.Controllers;

[ApiController, Authorize, Route("api/lists")]
public class ListApiController(SVContext db, IDService ids, PartyService parties, SongTransformer songs) : ControllerBase
{
    private object View(Playlist list) => new { id = ids.ToHash(list.Id), list.Title,
        ownerId = ids.ToHash(list.Owner), songs = list.Songs.Select(songs.ToApiSong) };

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var lists = await db.PlayLists.Where(p => p.Owner == ids.UserId(this)).Include(p => p.Songs).ThenInclude(s => s.ExternalSongs).ToListAsync();
        return Ok(lists.Select(View));
    }

    [HttpGet("favourites")]
    public async Task<IActionResult> Favourites()
    {
        using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var list = await parties.Favourites(await db.Users.FindAsync(ids.UserId(this)));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(View(list));
    }

    [HttpGet("{listId}")]
    public async Task<IActionResult> Get(string listId)
    {
        var list = await Find(listId);
        return list == null ? NotFound() : Ok(View(list));
    }

    [HttpPost("{listId}/songs")]
    public async Task<IActionResult> Add(string listId, [FromBody, Required] Models.SongId songId)
    {
        using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var list = await Find(listId);
        if (list == null) return NotFound();
        var song = await db.Songs.Include(s => s.ExternalSongs).FirstOrDefaultAsync(s => s.Id == ids.FromHash(songId.Id));
        if (song == null) return NotFound("Song not found.");
        if (list.Songs.All(s => s.Id != song.Id)) {
            if (list.Songs.Count >= PartyService.FavouriteLimit) return BadRequest("Choose up to 30 favourites. Remove one to make room.");
            list.Songs.Add(song);
        }
        var user = await db.Users.FindAsync(ids.UserId(this));
        var party = await parties.GetUserParty(user, true);
        if (party != null) await parties.Add(party, user, [song.Id]);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(View(list));
    }

    [HttpDelete("{listId}/songs/{songId}")]
    public async Task<IActionResult> Remove(string listId, string songId)
    {
        var list = await Find(listId);
        if (list == null) return NotFound();
        var song = list.Songs.FirstOrDefault(s => s.Id == ids.FromHash(songId));
        if (song != null) list.Songs.Remove(song);
        var user = await db.Users.FindAsync(ids.UserId(this));
        var party = await parties.GetUserParty(user, true);
        if (party != null) {
            var entry = (await parties.Songs(party)).FirstOrDefault(s => s.SongId == ids.FromHash(songId));
            entry?.UpVoters.Remove(user);
        }
        await db.SaveChangesAsync();
        return Ok(View(list));
    }

    private Task<Playlist> Find(string id) => db.PlayLists.Where(p => p.Owner == ids.UserId(this) && p.Id == ids.FromHash(id))
        .Include(p => p.Songs).ThenInclude(s => s.ExternalSongs).FirstOrDefaultAsync();
}
