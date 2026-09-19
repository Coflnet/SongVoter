using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Middleware;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coflnet.SongVoter.Service;

public class SongCatalog(SVContext db, IEnumerable<IMusicCatalog> providers, ILogger<SongCatalog> logger)
{
    public static (Platforms Platform, string Id) ParseLink(string input)
    {
        if (Regex.IsMatch(input, "^spotify:track:[A-Za-z0-9]{22}$")) return (Platforms.Spotify, input.Split(':')[2]);
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new ApiException(HttpStatusCode.BadRequest, "Paste a YouTube or Spotify song link.");
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string id = null;
        if (uri.Host is "open.spotify.com" && parts.Length >= 2 && parts[^2] == "track" && Regex.IsMatch(parts[^1], "^[A-Za-z0-9]{22}$"))
            return (Platforms.Spotify, parts[^1]);
        if (uri.Host == "youtu.be" && parts.Length == 1) id = parts[0];
        else if (uri.Host is "youtube.com" or "www.youtube.com" or "music.youtube.com" or "m.youtube.com") {
            if (parts.Length == 1 && parts[0] == "watch") id = QueryHelpers.ParseQuery(uri.Query).TryGetValue("v", out var value) ? value.ToString() : null;
            else if (parts.Length == 2 && parts[0] is "shorts" or "embed" or "live") id = parts[1];
        }
        if (id != null && Regex.IsMatch(id, "^[A-Za-z0-9_-]{11}$")) return (Platforms.Youtube, id);
        throw new ApiException(HttpStatusCode.BadRequest, "Use a link to a song or video on YouTube or Spotify.");
    }

    public async Task<Song> Resolve(Platforms platform, string id)
    {
        var existing = await Find(platform, id);
        if (existing != null) return existing;
        var provider = providers.SingleOrDefault(p => p.Platform == platform && p.IsConfigured)
            ?? throw new ApiException(HttpStatusCode.ServiceUnavailable, $"{platform} search is temporarily unavailable.");
        ExternalSong external;
        try { external = await provider.Resolve(id); }
        catch (Exception e) when (e is SpotifyAPI.Web.APIException or Google.GoogleApiException) {
            logger.LogWarning("{Platform} lookup failed: {Error}", platform, e.GetType().Name);
            throw new ApiException(HttpStatusCode.BadGateway, $"{platform} could not load that song. Try another link.");
        }
        if (external == null) throw new ApiException(HttpStatusCode.NotFound, "This song is unavailable for playback.");
        return await Store(external);
    }

    public async Task<(List<Song> Songs, List<string> Warnings)> Search(string term)
    {
        var lookup = Lookup(term);
        var found = await db.Songs.Where(s => s.Lookup.Contains(lookup)).Include(s => s.ExternalSongs).Take(20).ToListAsync();
        var warnings = new List<string>();
        foreach (var provider in providers) {
            if (!provider.IsConfigured) { warnings.Add($"{provider.Platform} search is not connected yet."); continue; }
            try {
                foreach (var external in await provider.Search(term)) {
                    var song = await Store(external);
                    if (found.All(s => s.Id != song.Id)) found.Add(song);
                }
            } catch (Exception e) when (e is SpotifyAPI.Web.APIException or Google.GoogleApiException or System.Net.Http.HttpRequestException) {
                logger.LogWarning("{Platform} search failed: {Error}", provider.Platform, e.GetType().Name);
                warnings.Add($"{provider.Platform} search is temporarily unavailable. You can still choose other results.");
            }
        }
        return (found, warnings);
    }

    private Task<Song> Find(Platforms platform, string id) => db.Songs.Include(s => s.ExternalSongs)
        .FirstOrDefaultAsync(s => s.ExternalSongs.Any(e => e.Platform == platform && e.ExternalId == id));

    private async Task<Song> Store(ExternalSong external)
    {
        using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        // The predicate read and insertion commit together, including concurrent imports.
        var song = await Find(external.Platform, external.ExternalId);
        if (song == null) {
            song = new Song { Title = external.Title, Lookup = Lookup(external.Title + external.Artist + external.ExternalId), ExternalSongs = [external] };
            db.Add(song);
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return song;
    }

    public static string Lookup(string value) => new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).Take(200).ToArray());
}
