using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Coflnet.SongVoter.Models;
using Coflnet.SongVoter.DBModels;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Google.Apis.YouTube.v3.Data;
using Google.Apis.YouTube.v3;
using System.Security.Cryptography.X509Certificates;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using System.Collections.Generic;
using SpotifyAPI.Web;
using SimplerConfig;

namespace Coflnet.SongVoter.Controllers.Impl
{
    public class SongApiControllerImpl : SongApiController
    {
        private readonly SVContext db;
        public SongApiControllerImpl(SVContext data)
        {
            this.db = data;
        }

        public override async Task<IActionResult> AddSong([FromBody] SongCreation body)
        {
            // TODO: check if they already exist
            IEnumerable<DBModels.Song> songs;
            if (body.Platform == SongCreation.PlatformEnum.SpotifyEnum)
                songs = new DBModels.Song[] { await GetSongFromSpotify() };
            else if (body.Platform == SongCreation.PlatformEnum.YoutubeEnum)
                songs = await GetSongDetailsFromYoutube(new string[] { body.ExternalId });
            else
                throw new APIException("You provided an unkown platform");

            foreach (var song in songs)
            {
                db.Add(song);
            }
            await db.SaveChangesAsync();

            return Ok(songs);
        }

        private static async Task<DBModels.Song> GetSongFromSpotify()
        {
            var config = SpotifyClientConfig
                          .CreateDefault()
                          .WithAuthenticator(new ClientCredentialsAuthenticator(SConfig.Instance["spotify:appid"], SConfig.Instance["spotify:secret"]));
            var spotify = new SpotifyClient(config);

            var track = await spotify.Tracks.Get("1sgKE7YUAhGDLNUU1ByEWu");

            var external = new DBModels.ExternalSong()
            {
                Artist = track.Artists.First().Name,
                ExternalId = track.Id,
                Platform = DBModels.ExternalSong.Platforms.Spotify,
                ThumbnailUrl = track.Album.Images.First().Url,
                Title = track.Album.Name
            };
            return new DBModels.Song()
            {
                ExternalSongs = new System.Collections.Generic.List<DBModels.ExternalSong>() { external },
                Title = track.Album.Name
            }; ;
        }

        private static async Task<IEnumerable<DBModels.Song>> GetSongDetailsFromYoutube(IEnumerable<string> ids)
        {
            var yt = new YouTubeService(
                        new BaseClientService.Initializer()
                        {
                            ApiKey = SConfig.Instance["youtube:apiKey"]
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
                    Platform = DBModels.ExternalSong.Platforms.Youtube,
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

        public override async Task<IActionResult> FindSong([FromQuery(Name = "term"), Required] string term)
        {
            var db = await this.db
                .Songs.Where(s => s.Title.StartsWith(term)).FirstOrDefaultAsync();

            return Ok(DBToApiSong(db));
        }

        public override async Task<IActionResult> GetSongById([FromRoute(Name = "songId"), Required] long songId)
        {
            var db = await this.db
                .Songs.Where(s => s.Id == songId).FirstOrDefaultAsync();

            return base.Ok(DBToApiSong(db));
        }

        private static Models.Song DBToApiSong(DBModels.Song db)
        {
            return new Models.Song()
            {
                Id = db.Id,
                Title = db.Title,
                Occurences = db.ExternalSongs.Select(s => new Models.ExternalSong()
                {
                    Artist = s.Artist,
                    ExternalId = s.ExternalId,
                    Platform = (Models.ExternalSong.PlatformEnum)s.Platform,
                    Thumbnail = s.ThumbnailUrl,
                    Title = s.Title
                }).ToList()
            };
        }
    }
}
