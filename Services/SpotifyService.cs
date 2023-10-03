using Coflnet.SongVoter.DBModels;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using SpotifyAPI.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace Coflnet.SongVoter.Service;

public class SpotifyService
{
    private SpotifyClient spotifyClient;
    private IConfiguration config;
    private readonly SVContext db;
    private readonly ILogger<SpotifyService> logger;

    public SpotifyService(IConfiguration config, ILogger<SpotifyService> logger, SVContext db)
    {
        this.config = config;
        this.logger = logger;
        this.db = db;
    }

    public async Task<DBModels.Song> GetSongFrom(string trackId, string searchTerm)
    {
        SpotifyClient spotify = GetSpotifyclient();

        var track = await spotify.Tracks.Get(trackId).ConfigureAwait(false);
        return ConvertSpotifyToDbSong(searchTerm, track);
    }

    public async Task<DBModels.Song[]> GetOrCreate(IList<string> trackIds)
    {
        var existing = await db.Songs.Where(s => s.ExternalSongs.Where(e => trackIds.Contains(e.ExternalId)).Any()).Include(t => t.ExternalSongs).ToListAsync();
        SpotifyClient spotify = GetSpotifyclient();
        var existingIds = existing.SelectMany(s => s.ExternalSongs.Select(e => e.ExternalId));
        var missingIds = trackIds.Except(existingIds).ToList();
        Song[] newTracks = new Song[0];
        if (missingIds.Count > 0)
        {
            var request = new TracksRequest(missingIds);
            var tracks = await spotify.Tracks.GetSeveral(request).ConfigureAwait(false);
            newTracks = tracks.Tracks.Select(t => ConvertSpotifyToDbSong(t.Name, t)).ToArray();
            db.Songs.AddRange(newTracks);
            await db.SaveChangesAsync();
        }
        return existing.Concat(newTracks).ToArray();
    }


    private SpotifyClient GetSpotifyclient()
    {
        if (spotifyClient != null)
            return spotifyClient;
        var config = SpotifyClientConfig
                                  .CreateDefault()
                                  .WithAuthenticator(new ClientCredentialsAuthenticator(this.config["spotify:clientid"], this.config["spotify:clientsecret"]));
        var spotify = new SpotifyClient(config);
        spotifyClient = spotify;
        return spotify;
    }



    private static DBModels.Song ConvertSpotifyToDbSong(string searchTerm, FullTrack track)
    {
        var external = new DBModels.ExternalSong()
        {
            Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
            ExternalId = track.Id,
            Platform = Platforms.Spotify,
            ThumbnailUrl = track.Album.Images.First().Url,
            Title = track.Name,
            Duration = TimeSpan.FromMilliseconds(track.DurationMs)
        };
        var combined = external.Title + external.Artist + external.ExternalId + searchTerm;
        return new DBModels.Song()
        {
            ExternalSongs = new System.Collections.Generic.List<DBModels.ExternalSong>() { external },
            Title = track.Name,
            Lookup = Controllers.SongApiController.ConvertLookupText(combined)
        };
    }



    public async Task<SearchResponse> Search(string term)
    {
        var spotify = GetSpotifyclient();
        var query = new SearchRequest(SearchRequest.Types.Track | SearchRequest.Types.Episode, term);
        query.Limit = 20;
        var spotifyResponse = await spotify.Search.Item(query);
        return spotifyResponse;
    }

    public async Task<List<string>> UpdateSongs(SearchResponse spotifyResponse, string searchTerm)
    {
        var spotifyIds = spotifyResponse.Tracks.Items.Select(i => i.Id);
        spotifyResponse.Tracks.Items.First().Album.Images.First().Url.ToString();
        var spotifyExisting = await db.ExternalSongs.Where(s => spotifyIds.Contains(s.ExternalId)).Select(e => e.ExternalId).ToListAsync();
        if (spotifyExisting.Count != spotifyIds.Count())
        {
            // execute in parallel
            foreach (var item in spotifyIds.Where(i => !spotifyExisting.Contains(i)))
            {
                try
                {
                    var songsToAdd = ConvertSpotifyToDbSong(searchTerm, spotifyResponse.Tracks.Items.First(i => i.Id == item));
                    db.Add(songsToAdd);
                }
                catch (System.Exception e)
                {
                    logger.LogError(e, "Error while parsing spotify song " + item);
                }
            }
            await db.SaveChangesAsync();
        }
        else
        {
            // update song titles TODO: move this to background job
            // var spotifySongs = await db.ExternalSongs.Where(s => spotifyIds.Contains(s.ExternalId)).ToListAsync();
            // foreach (var song in spotifySongs)
            // {
            //     var item = spotifyResponse.Tracks.Items.First(i => i.Id == song.ExternalId);
            //     song.Title = item.Name;
            //     song.ThumbnailUrl = item.Album.Images.First().Url;
            //     song.Artist = item.Artists.First().Name;
            //     song.Duration = TimeSpan.FromMilliseconds(item.DurationMs);
            // }
            // await db.SaveChangesAsync();
        }
        return spotifyIds.ToList();
    }
}
