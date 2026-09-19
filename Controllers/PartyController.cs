using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Coflnet.SongVoter.Transformers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.SongVoter.Controllers;

[ApiController, Authorize, Route("api/party")]
public class PartyController(SVContext db, IDService ids, SongTransformer transformer, PartyService parties) : ControllerBase
{
    public record CreateRequest([Required, StringLength(30, MinimumLength = 1)] string Name,
        [Required, MinLength(1)] Models.SongPlatform[] SupportedPlatforms);
    public record AdvanceRequest([Range(0, int.MaxValue)] int Version);
    private async Task<User> CurrentUser() => await db.Users.FindAsync(ids.UserId(this));

    private async Task<object> Snapshot(Party party, User user)
    {
        var songs = await parties.Songs(party);
        var invite = await db.Invites.Where(i => i.Party == party && i.ValidUntil > DateTime.UtcNow).OrderByDescending(i => i.ValidUntil).FirstOrDefaultAsync();
        return new {
            id = ids.ToHash(party.Id), party.Name, ownerId = ids.ToHash(party.Creator.Id),
            members = party.Members.Count + 1, platforms = transformer.SplitPlatforms(party.SupportedPlatforms),
            joinUrl = invite == null ? null : $"https://songvoter.party/join/{invite.Code}",
            code = invite?.Code, version = party.PlaybackVersion,
            currentSong = transformer.ToApiSong(songs.FirstOrDefault(s => s.SongId == party.CurrentSongId)?.Song),
            queue = PartyService.Ranked(party, songs).Select(s => new {
                song = transformer.ToApiSong(s.Song), score = Math.Round(PartyService.Score(s, party, songs), 3),
                votes = s.UpVoters.Count(u => u.Id == party.Creator.Id || party.Members.Any(m => m.Id == u.Id)),
                favourite = s.UpVoters.Any(u => u.Id == user.Id), played = s.PlayedTimes,
                playable = s.Song.ExternalSongs.Any(e => party.SupportedPlatforms.HasFlag(e.Platform))
            })
        };
    }

    private Invite NewInvite(Party party, User owner) => new() {
        Party = party, CreatorId = owner.Id, Code = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6)),
        ValidUntil = DateTime.UtcNow.AddDays(7)
    };

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await CurrentUser();
        return Ok(await Snapshot(await parties.GetUserParty(user), user));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateRequest request)
    {
        var user = await CurrentUser();
        if (await parties.GetUserParty(user, true) != null) return Conflict("Leave your current party first.");
        var platforms = transformer.CombinePlatforms(request.SupportedPlatforms);
        if ((platforms & ~(Models.SongPlatform.Youtube | Models.SongPlatform.Spotify)) != 0 || platforms == 0)
            return BadRequest("Choose YouTube, Spotify, or both.");
        var party = new Party { Creator = user, Name = request.Name.Trim(), SupportedPlatforms = (Platforms)platforms, Members = [] };
        if (party.Name.Length == 0) return BadRequest("Give your party a name.");
        using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        db.Add(party);
        db.Add(NewInvite(party, user));
        await db.SaveChangesAsync();
        var favourites = await parties.Favourites(user);
        await parties.Add(party, user, favourites.Songs.Take(PartyService.FavouriteLimit).Select(s => s.Id));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(await Snapshot(party, user));
    }

    [HttpPost("{code}/join")]
    public async Task<IActionResult> Join(string code)
    {
        var user = await CurrentUser();
        using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var invite = await db.Invites.Include(i => i.Party).ThenInclude(p => p.Creator)
            .Include(i => i.Party).ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(i => i.Code == code && i.ValidUntil > DateTime.UtcNow && (i.UsageLimit == 0 || i.UsageCount < i.UsageLimit));
        if (invite == null) return NotFound("This invite has expired or the party has ended.");
        var current = await parties.GetUserParty(user, true);
        if (current != null && current.Id != invite.Party.Id) return Conflict("Leave your current party before joining another.");
        var party = invite.Party;
        if (party.Creator.Id != user.Id && !party.Members.Contains(user)) {
            if (party.Members.Count >= 200) return BadRequest("This party is full.");
            party.Members.Add(user);
            invite.UsageCount++;
            var favourites = await parties.Favourites(user);
            await parties.Add(party, user, favourites.Songs.Take(PartyService.FavouriteLimit).Select(s => s.Id));
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return Ok(await Snapshot(party, user));
    }

    [HttpPost("inviteLink")]
    public async Task<IActionResult> RefreshInvite()
    {
        var user = await CurrentUser();
        var party = await parties.GetUserParty(user);
        if (party.Creator.Id != user.Id) return Forbid();
        db.Invites.RemoveRange(await db.Invites.Where(i => i.Party == party).ToListAsync());
        db.Add(NewInvite(party, user));
        await db.SaveChangesAsync();
        return Ok(await Snapshot(party, user));
    }

    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody, MinLength(1), MaxLength(30)] List<string> songIds)
    {
        var user = await CurrentUser();
        using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var party = await parties.GetUserParty(user);
        var favourites = await parties.Favourites(user);
        var newIds = songIds.Select(ids.FromHash).Distinct().Except(favourites.Songs.Select(s => s.Id)).ToArray();
        if (favourites.Songs.Count + newIds.Length > PartyService.FavouriteLimit) return BadRequest("Your favourites are full. Remove a song before adding another.");
        await parties.Add(party, user, songIds.Select(ids.FromHash));
        foreach (var song in await db.Songs.Where(s => newIds.Contains(s.Id)).ToListAsync()) favourites.Songs.Add(song);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(await Snapshot(party, user));
    }

    [HttpPost("removeVote/{songId}")]
    public async Task<IActionResult> RemoveVote(string songId)
    {
        var user = await CurrentUser();
        var party = await parties.GetUserParty(user);
        var entry = (await parties.Songs(party)).FirstOrDefault(s => s.SongId == ids.FromHash(songId));
        if (entry == null) return NotFound();
        entry.UpVoters.Remove(user);
        entry.DownVoters.Remove(user);
        var favourites = await parties.Favourites(user);
        var song = favourites.Songs.FirstOrDefault(s => s.Id == entry.SongId);
        if (song != null) favourites.Songs.Remove(song);
        await db.SaveChangesAsync();
        return Ok(await Snapshot(party, user));
    }

    [HttpPost("next")]
    public async Task<IActionResult> Next(AdvanceRequest request)
    {
        var user = await CurrentUser();
        var party = await parties.GetUserParty(user);
        if (party.Creator.Id != user.Id) return Forbid();
        using var transaction = await db.Database.BeginTransactionAsync();
        // Claim this transition before touching play counts. A delayed SDK callback cannot skip a track.
        if (await db.Parties.Where(p => p.Id == party.Id && p.PlaybackVersion == request.Version)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.PlaybackVersion, p => p.PlaybackVersion + 1)) != 1)
            return Conflict("Playback has already advanced. Refresh the queue.");
        var songs = await parties.Songs(party);
        var candidates = PartyService.Ranked(party, songs).Where(s =>
            s.Song.ExternalSongs.Any(e => party.SupportedPlatforms.HasFlag(e.Platform)) && PartyService.Score(s, party, songs) > 0).ToList();
        var next = candidates.FirstOrDefault(s => s.SongId != party.CurrentSongId) ?? candidates.FirstOrDefault();
        party.CurrentSongId = next?.SongId;
        if (next != null) next.PlayedTimes++;
        // ExecuteUpdate bypasses tracking; align the version before persisting the current song.
        party.PlaybackVersion = request.Version + 1;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(await Snapshot(party, user));
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave()
    {
        await parties.LeaveParty(await CurrentUser());
        return NoContent();
    }

    [HttpPost("kick/{userId}")]
    public async Task<IActionResult> Kick(string userId)
    {
        var owner = await CurrentUser();
        var party = await parties.GetUserParty(owner);
        if (party.Creator.Id != owner.Id) return Forbid();
        var member = party.Members.FirstOrDefault(m => m.Id == ids.FromHash(userId));
        if (member == null) return NotFound();
        party.Members.Remove(member);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
