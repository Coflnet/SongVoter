using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.SongVoter.Service;

public class PartyService(SVContext db)
{
    public const int FavouriteLimit = 30;
    public async Task<Party> GetUserParty(User user, bool allowNull = false)
    {
        var party = await db.Parties.Include(p => p.Members).Include(p => p.Creator)
            .FirstOrDefaultAsync(p => p.Creator == user || p.Members.Contains(user));
        if (party == null && !allowNull) throw new ApiException(HttpStatusCode.NotFound, "Join a party to see its queue.");
        return party;
    }

    public Task<List<PartySong>> Songs(Party party) => db.PartySongs.Where(s => s.PartyId == party.Id)
        .Include(s => s.UpVoters).Include(s => s.DownVoters).Include(s => s.Song).ThenInclude(s => s.ExternalSongs).ToListAsync();

    public static double Score(PartySong song, Party party, IReadOnlyCollection<PartySong> queue)
    {
        var present = party.Members.Select(m => m.Id).Append(party.Creator.Id).ToHashSet();
        double Weight(User user) => (user.Id == party.Creator.Id || !user.IsAnonymous ? 3.0 : 1.0)
            / Math.Max(1, queue.Count(s => s.UpVoters.Any(u => u.Id == user.Id) || s.DownVoters.Any(u => u.Id == user.Id)));
        return song.UpVoters.Where(u => present.Contains(u.Id)).Sum(Weight)
            - song.DownVoters.Where(u => present.Contains(u.Id)).Sum(Weight);
    }

    public static IEnumerable<PartySong> Ranked(Party party, IReadOnlyCollection<PartySong> songs) => songs
        .OrderBy(s => s.PlayedTimes).ThenByDescending(s => Score(s, party, songs)).ThenBy(s => s.Id);

    public async Task<Playlist> Favourites(User user)
    {
        var list = await db.PlayLists.Where(p => p.Owner == user.Id).Include(p => p.Songs).ThenInclude(s => s.ExternalSongs)
            .OrderBy(p => p.Id).FirstOrDefaultAsync();
        if (list != null) return list;
        list = new Playlist { Owner = user.Id, Title = "Favourites", Songs = new List<Song>() };
        db.Add(list);
        return list;
    }

    public async Task Add(Party party, User user, IEnumerable<int> ids)
    {
        var songIds = ids.Distinct().ToArray();
        var songs = await db.Songs.Where(s => songIds.Contains(s.Id)).Include(s => s.ExternalSongs).ToListAsync();
        if (songs.Count != songIds.Length) throw new ApiException(HttpStatusCode.NotFound, "Song not found.");
        var queue = await Songs(party);
        if (queue.Count + songs.Count(s => queue.All(q => q.SongId != s.Id)) > 500)
            throw new ApiException(HttpStatusCode.BadRequest, "This party's queue is full.");
        if (queue.Count(q => q.UpVoters.Contains(user)) + songs.Count(s => !queue.Any(q => q.SongId == s.Id && q.UpVoters.Contains(user))) > FavouriteLimit)
            throw new ApiException(HttpStatusCode.BadRequest, $"Choose up to {FavouriteLimit} favourites for this party.");
        foreach (var song in songs)
        {
            var entry = queue.FirstOrDefault(s => s.SongId == song.Id);
            if (entry == null) {
                entry = new PartySong { Party = party, Song = song };
                db.Add(entry);
                queue.Add(entry);
            }
            entry.DownVoters.Remove(user);
            if (!entry.UpVoters.Contains(user)) entry.UpVoters.Add(user);
        }
    }

    public async Task LeaveParty(User user)
    {
        var party = await GetUserParty(user);
        if (party.Creator.Id == user.Id) {
            db.Invites.RemoveRange(await db.Invites.Where(i => i.Party == party).ToListAsync());
            db.Parties.Remove(party);
        } else party.Members.Remove(user);
        await db.SaveChangesAsync();
    }
}
