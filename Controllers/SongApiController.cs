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

namespace Coflnet.SongVoter.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    public class SongApiControllerImpl : ControllerBase
    {
        private readonly SVContext db;
        private IDService idService;
        private IConfiguration config;
        private SongTransformer transformer;
        public SongApiControllerImpl(SVContext data, IConfiguration config, IDService iDService, SongTransformer transformer)
        {
            this.db = data;
            idService = iDService;
            this.config = config;
            this.transformer = transformer;
        }

        /// <summary>
        /// Add a new song by url
        /// </summary>
        /// <param name="body">Song object that needs to be added to the store</param>
        /// <response code="405">Invalid input</response>
        [HttpPost]
        [Route("/songs")]
        [Authorize]
        [Consumes("application/json")]
        [ValidateModelState]
        [SwaggerOperation("AddSong")]
        public async Task<IActionResult> AddSong([FromBody] SongCreation body)
        {
            if(db.ExternalSongs.Where(s=>s.ExternalId == body.ExternalId).Any())
                return StatusCode(409,"Song already exists");
            IEnumerable<DBModels.Song> songs;
            if (body.Platform == SongCreation.PlatformEnum.SpotifyEnum)
                songs = new DBModels.Song[] { await GetSongFromSpotify() };
            else if (body.Platform == SongCreation.PlatformEnum.YoutubeEnum)
                songs = await GetSongDetailsFromYoutube(new string[] { body.ExternalId });
            else
                throw new ApiException(System.Net.HttpStatusCode.BadRequest,"The field `platform` in your request is invalid");

            foreach (var song in songs)
            {
                db.Add(song);
            }
            await db.SaveChangesAsync();

            return Ok(transformer.ToApiSong(songs.First()));
        }

        private async Task<DBModels.Song> GetSongFromSpotify()
        {
            var config = SpotifyClientConfig
                          .CreateDefault()
                          .WithAuthenticator(new ClientCredentialsAuthenticator(this.config["spotify:appid"], this.config["spotify:secret"]));
            var spotify = new SpotifyClient(config);

            var track = await spotify.Tracks.Get("1sgKE7YUAhGDLNUU1ByEWu");

            var external = new DBModels.ExternalSong()
            {
                Artist = track.Artists.First().Name,
                ExternalId = track.Id,
                Platform = Platforms.Spotify,
                ThumbnailUrl = track.Album.Images.First().Url,
                Title = track.Album.Name
            };
            return new DBModels.Song()
            {
                ExternalSongs = new System.Collections.Generic.List<DBModels.ExternalSong>() { external },
                Title = track.Album.Name
            }; ;
        }

        private async Task<IEnumerable<DBModels.Song>> GetSongDetailsFromYoutube(IEnumerable<string> ids)
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

                var external = new DBModels.ExternalSong()
                {
                    Artist = ytVideo.Snippet.ChannelTitle,
                    ExternalId = ytVideo.Id,
                    Platform = Platforms.Youtube,
                    ThumbnailUrl = ytVideo.Snippet.Thumbnails.Standard.Url,
                    Title = ytVideo.Snippet.Title
                };

                return new DBModels.Song()
                {
                    ExternalSongs = new System.Collections.Generic.List<DBModels.ExternalSong>() { external },
                    Title = ytVideo.Snippet.Title
                };
            });
        }

        /// <summary>
        /// Finds Song by search term
        /// </summary>
        /// <param name="term">Search term to serach for</param>
        /// <response code="200">successful operation</response>
        /// <response code="400">Invalid search term</response>
        [HttpGet]
        [Route("/songs/search")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("FindSong")]
        [SwaggerResponse(statusCode: 200, type: typeof(List<Models.Song>), description: "successful operation")]
        public async Task<IActionResult> FindSong([FromQuery(Name = "term"), Required] string term)
        {
            var songs = await this.db
                .Songs.Where(s => s.Title.StartsWith(term))
                .Include(s=>s.ExternalSongs)
                .Take(20)
                .ToListAsync();

            return Ok(songs.Select(s=>transformer.ToApiSong(s)));
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
        [Route("/song/{songId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("GetSongById")]
        [SwaggerResponse(statusCode: 200, type: typeof(Models.Song), description: "successful operation")]
        public async Task<IActionResult> GetSongById([FromRoute(Name = "songId"), Required] string songId)
        {
            var numericalId = idService.FromHash(songId);
            var db = await this.db
                .Songs.Where(s => s.Id == numericalId)
                .Include(s=>s.ExternalSongs)
                .FirstOrDefaultAsync();

            return base.Ok(transformer.ToApiSong(db));
        }

       
    }
}
