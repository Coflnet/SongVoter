using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.SongVoter.Controllers;

[ApiController, Authorize, Route("api/import"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ImportController(SVContext db, IDService ids, MusicImport music, SongCatalog catalog, PartyService parties) : ControllerBase
{
    public record ConnectRequest([Required, RegularExpression("^[a-f0-9]{64}$")] string Proof, bool Native, [RegularExpression("^(de|en)$")] string Language = "en");
    public record CompleteRequest([Required, StringLength(100)] string State, [Required, RegularExpression("^[a-f0-9]{64}$")] string Proof);
    public record ImportRequest([StringLength(100)] string ListId, [StringLength(1000)] string Url);

    [HttpGet("{provider}/lists")]
    public async Task<IActionResult> Lists(string provider, [FromQuery, StringLength(500)] string cursor = null)
    {
        MusicImport.Platform(provider);
        var marker = MusicImport.Marker(provider);
        if (!await db.Set<Oauth2Token>().AnyAsync(t => t.User.Id == ids.UserId(this) && t.ExternalId == marker))
            return Ok(new { connected = false, available = music.Configured(provider), lists = Array.Empty<object>(), next = (string)null });
        var page = await music.Lists(ids.UserId(this), provider, cursor);
        return Ok(new { connected = true, available = true, page.Lists, page.Next });
    }

    [HttpPost("{provider}/connect"), EnableRateLimiting("imports")]
    public async Task<IActionResult> Connect(string provider, ConnectRequest request)
    {
        var result = await music.Start(await db.Users.FindAsync(ids.UserId(this)), provider, request.Proof, request.Native, request.Language ?? "en");
        return Ok(new { result.Url, result.State });
    }

    [HttpGet("{provider}/callback"), AllowAnonymous]
    public async Task<IActionResult> Callback(string provider, [StringLength(100)] string state, [StringLength(4000)] string code = null, [StringLength(500)] string error = null) =>
        Redirect(await music.CallbackResult(provider, state, code, error));

    [HttpPost("{provider}/complete")]
    public async Task<IActionResult> Complete(string provider, CompleteRequest request)
    {
        await music.Complete(ids.UserId(this), provider, request.State, request.Proof);
        return NoContent();
    }

    [HttpDelete("{provider}")]
    public async Task<IActionResult> Disconnect(string provider)
    {
        var platform = MusicImport.Platform(provider);
        // Also cancel unfinished authorizations owned by this profile. Other/legacy tokens are untouched.
        await db.Set<Oauth2Token>().Where(t => t.User.Id == ids.UserId(this) && t.Platform == platform &&
            (t.ExternalId == MusicImport.Marker(provider) || t.ExternalId.StartsWith("songvoter-import-pending:"))).ExecuteDeleteAsync();
        return NoContent();
    }

    [HttpPost("{provider}"), EnableRateLimiting("imports")]
    public async Task<IActionResult> Import(string provider, ImportRequest request)
    {
        var platform = MusicImport.Platform(provider);
        var listId = request.ListId;
        if (request.Url != null) {
            var parsed = MusicImport.ParsePlaylist(request.Url);
            if (parsed.Provider != provider) return BadRequest("Choose the matching music service for this playlist.");
            listId = parsed.Id;
        }
        if (string.IsNullOrWhiteSpace(listId)) return BadRequest("Choose a playlist or paste its full link.");
        var user = await db.Users.FindAsync(ids.UserId(this));
        var favourites = await parties.Favourites(user);
        await db.SaveChangesAsync();
        var remaining = PartyService.FavouriteLimit - favourites.Songs.Count;
        if (remaining <= 0) return BadRequest("Your favourites are full. Remove a song before adding another.");
        var seen = favourites.Songs.SelectMany(s => s.ExternalSongs).Where(s => s.Platform == platform).Select(s => s.ExternalId).ToHashSet();
        var imported = new List<Song>();
        string cursor = null;
        // Bound provider work even for playlists containing thousands of unavailable/duplicate entries.
        for (var page = 0; page < 6; page++) {
            var result = await music.ReadSongs(user.Id, provider, listId, cursor, request.Url != null);
            foreach (var external in result.Songs) {
                if (!seen.Add(external.ExternalId)) continue;
                imported.Add(await catalog.Store(external));
                if (imported.Count == remaining) break;
            }
            cursor = result.Next;
            if (cursor == null || imported.Count == remaining) break;
        }
        var importedIds = imported.Select(s => s.Id).ToArray();
        db.ChangeTracker.Clear();
        using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        // Re-read membership inside the transaction so concurrent imports cannot exceed the cap.
        user = await db.Users.FindAsync(ids.UserId(this));
        favourites = await parties.Favourites(user);
        imported = await db.Songs.Where(s => importedIds.Contains(s.Id)).OrderBy(s => s.Id).ToListAsync();
        var additions = imported.Where(s => favourites.Songs.All(f => f.Id != s.Id)).Take(Math.Max(0, PartyService.FavouriteLimit - favourites.Songs.Count)).ToArray();
        foreach (var song in additions) favourites.Songs.Add(song);
        var party = await parties.GetUserParty(user, true);
        if (party != null) await parties.Add(party, user, additions.Select(s => s.Id));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { added = additions.Length, total = favourites.Songs.Count, limit = PartyService.FavouriteLimit,
            limitReached = favourites.Songs.Count >= PartyService.FavouriteLimit, moreAvailable = cursor != null });
    }
}
