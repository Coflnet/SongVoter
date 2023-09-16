using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Coflnet.SongVoter.Models;
using Coflnet.SongVoter.DBModels;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using System.Collections.Generic;
using SpotifyAPI.Web;
using Coflnet.SongVoter.Service;
using Coflnet.SongVoter.Middleware;
using Coflnet.SongVoter.Transformers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Coflnet.SongVoter.Attributes;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.Extensions.Logging;
using System;
using Google.Apis.YouTube.v3.Data;
using System.Text.RegularExpressions;

namespace Coflnet.SongVoter.Controllers;
/// <summary>
/// 
/// </summary>
[Route("api/songs")]
public class SongApiController : ControllerBase
{
    private readonly SVContext db;
    private IDService idService;
    private IConfiguration config;
    private ILogger<SongApiController> logger;
    private SongTransformer transformer;
    private SpotifyClient spotifyClient;
    public SongApiController(SVContext data, IConfiguration config, IDService iDService, SongTransformer transformer, ILogger<SongApiController> logger)
    {
        this.db = data;
        idService = iDService;
        this.config = config;
        this.transformer = transformer;
        this.logger = logger;
    }

    /// <summary>
    /// Add a new song by url
    /// </summary>
    /// <param name="body">Song object that needs to be added to the store</param>
    /// <response code="405">Invalid input</response>
    [HttpPost]
    [Route("")]
    [Authorize]
    [Consumes("application/json")]
    [ValidateModelState]
    [SwaggerOperation("AddSong")]
    public async Task<IActionResult> AddSong([FromBody] SongCreation body)
    {
        if (db.ExternalSongs.Where(s => s.ExternalId == body.ExternalId).Any())
            return StatusCode(409, "Song already exists");
        IEnumerable<DBModels.Song> songs;
        if (body.Platform == SongPlatform.Spotify)
            songs = new DBModels.Song[] { await GetSongFromSpotify(null, null) };
        else if (body.Platform == SongPlatform.Youtube)
            songs = await GetSongDetailsFromYoutube(new string[] { body.ExternalId }, null);
        else
            throw new ApiException(System.Net.HttpStatusCode.BadRequest, "The field `platform` in your request is invalid");

        foreach (var song in songs)
        {
            db.Add(song);
        }
        await db.SaveChangesAsync();

        return Ok(transformer.ToApiSong(songs.First()));
    }

    private async Task<DBModels.Song> GetSongFromSpotify(string trackId, string searchTerm)
    {
        SpotifyClient spotify = GetSpotifyclient();

        var track = await spotify.Tracks.Get(trackId).ConfigureAwait(false);
        return ConvertSpotifyToDbSong(searchTerm, track);
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
            Lookup = ConvertLookupText(combined)
        };
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

    public static string ConvertLookupText(string combined)
    {
        var full = Regex.Replace(combined, "[^a-zA-Z0-9]", "").ToLower();
        if (full.Length > 200)
            return full.Substring(0, 200);
        return full;
    }

    private async Task<IEnumerable<DBModels.Song>> GetSongDetailsFromYoutube(IEnumerable<string> ids, string searchTerm)
    {
        var yt = new YouTubeService(
                    new BaseClientService.Initializer()
                    {
                        ApiKey = config["youtube:apiKey"]
                    });


        var request = yt.Videos.List("contentDetails,snippet,status");
        request.Id = new Google.Apis.Util.Repeatable<string>(ids);

        var response = await request.ExecuteAsync();

        return response.Items.Select(ytVideo =>
        {

            try
            {

                var external = new DBModels.ExternalSong()
                {
                    Artist = ytVideo.Snippet.ChannelTitle,
                    ExternalId = ytVideo.Id,
                    Platform = Platforms.Youtube,
                    ThumbnailUrl = (ytVideo.Snippet?.Thumbnails?.Standard ?? ytVideo.Snippet?.Thumbnails?.High)?.Url,
                    Title = ytVideo.Snippet.Title,
                    // sample PT2M2S
                    Duration = System.Xml.XmlConvert.ToTimeSpan(ytVideo.ContentDetails.Duration)
                };

                return new DBModels.Song()
                {
                    ExternalSongs = new System.Collections.Generic.List<DBModels.ExternalSong>() { external },
                    Title = ytVideo.Snippet.Title,
                    Lookup = ConvertLookupText(ytVideo.Snippet.Title + ytVideo.Snippet.ChannelTitle + ytVideo.Id + searchTerm)
                };
            }
            catch (System.Exception e)
            {
                logger.LogError(e, "Error while parsing youtube video " + JsonConvert.SerializeObject(ytVideo, Formatting.Indented));
                throw;
            }
        });
    }

    /// <summary>
    /// Finds Song by search term
    /// </summary>
    /// <param name="term">Search term to serach for</param>
    /// <response code="200">successful operation</response>
    /// <response code="400">Invalid search term</response>
    [HttpGet]
    [Route("search")]
    [Authorize]
    [ValidateModelState]
    [SwaggerOperation("FindSong")]
    [SwaggerResponse(statusCode: 200, type: typeof(List<Models.Song>), description: "successful operation")]
    public async Task<IActionResult> FindSong([FromQuery(Name = "term"), Required] string term, SongPlatform[] platforms = null)
    {
        SongPlatform combinedPlatforms = transformer.CombinePlatforms(platforms);
        var localRes = await SearchLocalDbFor(term, combinedPlatforms);
        if (localRes.Count() > 12)
        {
            return Ok(localRes);
        }
        var internalpaltfomr = (SongVoter.DBModels.Platforms)combinedPlatforms;
        // search for song on youtube api
        var youtubeSearchTask = GetYoutubeSearchResult(term);
        SearchResponse spotifyResponse = await SearchSpotify(term);
        var spotifySongIds = await UpdateSpotifySongs(spotifyResponse, term);
        var youtubesongsIds = await UpdateYoutubeSongs(await youtubeSearchTask, term);
        var combinedSongIds = spotifySongIds.Concat(youtubesongsIds);
        var dbSongs = await db.Songs.Where(s => s.ExternalSongs.Any(e => combinedSongIds.Contains(e.ExternalId))).Include(s => s.ExternalSongs).ToListAsync();
        var songsToRespond = dbSongs
                .Where(s => s.ExternalSongs.Any(e => internalpaltfomr.HasFlag(e.Platform)));
        return Ok(songsToRespond.Select(s => transformer.ToApiSong(s)));
    }



    private async Task<SearchResponse> SearchSpotify(string term)
    {
        var spotify = GetSpotifyclient();
        var query = new SearchRequest(SearchRequest.Types.Track | SearchRequest.Types.Episode, term);
        query.Limit = 20;
        var spotifyResponse = await spotify.Search.Item(query);
        return spotifyResponse;
    }

    private async Task<List<string>> UpdateSpotifySongs(SearchResponse spotifyResponse, string searchTerm)
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

    private async Task<List<string>> UpdateYoutubeSongs(SearchListResponse response, string term)
    {
        // convert response to db entry 
        var ids = response.Items.Select(i => i.Id.VideoId);
        // filter for existing songs
        var existing = await db.ExternalSongs.Where(s => ids.Contains(s.ExternalId)).Select(e => e.ExternalId).ToListAsync();
        if (existing.Count != ids.Count())
        {
            var songsToAdd = await GetSongDetailsFromYoutube(ids.Except(existing), term);
            foreach (var song in songsToAdd)
            {
                db.Add(song);
            }
            await db.SaveChangesAsync();
        }
        // update song titles 
        // var songs = await db.ExternalSongs.Where(s => ids.Contains(s.ExternalId)).ToListAsync();
        // foreach (var song in songs)
        // {
        //     var item = response.Items.First(i => i.Id.VideoId == song.ExternalId);
        //     song.Title = item.Snippet.Title;
        //     song.ThumbnailUrl = (item.Snippet?.Thumbnails?.Standard ?? item.Snippet?.Thumbnails?.High)?.Url;
        //     song.Artist = item.Snippet.ChannelTitle;
        // }
        // await db.SaveChangesAsync();
        return ids.ToList();
    }

    private async Task<SearchListResponse> GetYoutubeSearchResult(string term)
    {
        Google.Apis.YouTube.v3.YouTubeService yt = new Google.Apis.YouTube.v3.YouTubeService(
                                new BaseClientService.Initializer()
                                {
                                    ApiKey = this.config["youtube:apiKey"]
                                });

        var request = yt.Search.List("snippet");
        request.Q = term;
        request.MaxResults = 20;
        request.Type = "video";

        var response = await request.ExecuteAsync();
        return response;
    }

    private async Task<IEnumerable<Models.Song>> SearchLocalDbFor(string term, SongPlatform platforms)
    {
        var lookup = ConvertLookupText(term);
        var songs = await this.db
            .Songs.Where(s => s.Lookup.ToLower().Contains(term.ToLower()))
            .Where(s => s.ExternalSongs.Any(e => (e.Platform == Platforms.Youtube && platforms.HasFlag(SongPlatform.Youtube)) || (e.Platform == Platforms.Spotify && platforms.HasFlag(SongPlatform.Spotify))))
            .Include(s => s.ExternalSongs)
            .Take(20)
            .ToListAsync();

        return songs.Select(s => transformer.ToApiSong(s));
    }

    /// <summary>
    /// Find song by ID
    /// </summary>
    /// <remarks>Returns a single song</remarks>
    /// <param name="songId">ID of song to return</param>
    /// <response code="200">successful operation</response>
    /// <response code="400">Invalid ID supplied</response>
    /// <response code="404">Song not found</response>
    [HttpGet]
    [Route("{songId}")]
    [Authorize]
    [ValidateModelState]
    [SwaggerOperation("GetSongById")]
    [SwaggerResponse(statusCode: 200, type: typeof(Models.Song), description: "successful operation")]
    public async Task<IActionResult> GetSongById([FromRoute(Name = "songId"), Required] string songId)
    {
        var numericalId = idService.FromHash(songId);
        var db = await this.db
            .Songs.Where(s => s.Id == numericalId)
            .Include(s => s.ExternalSongs)
            .FirstOrDefaultAsync();

        return base.Ok(transformer.ToApiSong(db));
    }


}
