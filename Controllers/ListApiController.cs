using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.Attributes;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Models;
using Coflnet.SongVoter.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Coflnet.SongVoter.Controllers
{
    public class ListApiControllerImpl : ControllerBase
    {
        private SVContext db;
        private IDService iDService;

        public ListApiControllerImpl(SVContext db, IDService idService)
        {
            this.db = db;
            iDService = idService;
        }

        /// <summary>
        /// Create a new playlist
        /// </summary>
        /// <param name="playList">An array of songIds to be added to the song</param>
        /// <response code="200">successful created list</response>
        [HttpPost]
        [Route("/lists")]
        [Authorize]
        [Consumes("application/json")]
        [ValidateModelState]
        [SwaggerOperation("CreatePlaylist")]
        [SwaggerResponse(statusCode: 200, type: typeof(PlayList), description: "successful created list")]
        public async Task<IActionResult> CreatePlaylist([FromBody] PlayListCreate playList)
        {
            var userId = GetUserId();
            var songIds = playList.Songs.Select(sid => iDService.FromHash(sid));
            var playlist = new DBModels.Playlist()
            {
                Owner = (int)userId,
                Title = playList.Title,
                Songs = db.Songs.Where(s => songIds.Contains(s.Id)).ToList()
            };
            db.Add(playlist);
            await db.SaveChangesAsync();
            return Ok(DBToApiPlaylist(playlist));
        }

        /// <summary>
        /// Adds a song to a playlist
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("/lists/{listId}/songs")]
        [Authorize]
        [Consumes("application/json")]
        [ValidateModelState]
        [SwaggerOperation("AddSongToList")]
        [SwaggerResponse(statusCode: 200, type: typeof(PlayList), description: "successful operation")]
        [SwaggerResponse(statusCode: 404, type: typeof(string), description: "song or playlist not found")]
        public async Task<IActionResult> AddSongToList([FromRoute(Name = "listId"), Required] string listId, [FromBody] SongId songId)
        {
            var dbId = iDService.FromHash(listId);
            var userId = GetUserId();
            var list = await db.PlayLists.Where(p => p.Id == dbId && p.Owner == userId).Include(p => p.Songs).FirstOrDefaultAsync();
            if (list == null)
            {
                return NotFound("list not found");
            }
            var song = await db.Songs.FindAsync((int)iDService.FromHash(songId.Id));
            if (song == null)
            {
                return NotFound("song not found");
            }
            list.Songs.Add(song);
            await db.SaveChangesAsync();
            return Ok(DBToApiPlaylist(list));
        }

        /// <summary>
        /// Removes a song from a playlist
        /// </summary>
        /// <returns></returns>
        /// <response code="200">successful operation</response>
        /// <response code="404">list or song not found</response>
        /// <response code="400">song not in list</response>
        /// <response code="401">user not authorized</response>
        [HttpDelete]
        [Route("/lists/{listId}/songs/{songId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("RemoveSongFromList")]
        [SwaggerResponse(statusCode: 200, type: typeof(PlayList), description: "successful operation")]
        [SwaggerResponse(statusCode: 404, type: typeof(string), description: "song or playlist not found")]
        [SwaggerResponse(statusCode: 400, type: typeof(string), description: "song not in list")]
        public async Task<IActionResult> RemoveSongFromList([FromRoute(Name = "listId"), Required] string listId, [FromRoute(Name = "songId"), Required] string songId)
        {
            var dbId = iDService.FromHash(listId);
            var userId = GetUserId();
            var list = await db.PlayLists.Where(p => p.Id == dbId && p.Owner == userId).Include(p => p.Songs).FirstOrDefaultAsync();
            if (list == null)
            {
                return NotFound("list not found");
            }
            var song = await db.Songs.FindAsync(iDService.FromHash(songId));
            if (song == null)
            {
                return NotFound("song not found");
            }
            if (!list.Songs.Contains(song))
            {
                return BadRequest("song not in list");
            }
            list.Songs.Remove(song);
            await db.SaveChangesAsync();
            return Ok(DBToApiPlaylist(list));
        }

        private long GetUserId()
        {
            return iDService.UserId(this);
        }
        /// <summary>
        /// Find playlist by ID
        /// </summary>
        /// <remarks>Returns a playList</remarks>
        /// <param name="listId">ID of list to return</param>
        /// <response code="200">successful operation</response>
        [HttpGet]
        [Route("/lists/{listId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("GetListById")]
        [SwaggerResponse(statusCode: 200, type: typeof(PlayList), description: "successful operation")]
        public async Task<IActionResult> GetListById([FromRoute(Name = "listId"), Required] string listId)
        {
            var dbId = iDService.FromHash(listId);
            var userId = GetUserId();
            var result = await db.PlayLists.Where(p => p.Id == dbId && p.Owner == userId)
                .Include(p => p.Songs).ThenInclude(s => s.ExternalSongs).FirstOrDefaultAsync();
            return base.Ok(DBToApiPlaylist(result));
        }



        /// <summary>
        /// Get playlist for active user
        /// </summary>
        /// <response code="200">successful response</response>
        [HttpGet]
        [Route("/lists")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("GetPlaylists")]
        [SwaggerResponse(statusCode: 200, type: typeof(List<PlayList>), description: "successful response")]
        public async Task<IActionResult> GetPlaylists()
        {
            var userId = GetUserId();
            var result = await db.PlayLists.Where(p => p.Owner == userId)
                .Include(p => p.Songs).ThenInclude(s => s.ExternalSongs).ToListAsync();
            return Ok(result.Select(p => DBToApiPlaylist(p)));
        }

        private PlayList DBToApiPlaylist(Playlist result)
        {
            return new Models.PlayList()
            {
                Id = iDService.ToHash(result.Id),
                Songs = result.Songs?.Select(s => new Models.Song()
                {
                    Id = iDService.ToHash(s.Id),
                    Title = s.Title,
                    Occurences = s.ExternalSongs?.Select(o => new Models.ExternalSong()
                    {
                        Platform = (Models.ExternalSong.PlatformEnum)o.Platform,
                        ExternalId = o.ExternalId,
                        Title = o.Title,
                        Artist = o.Artist,
                        Thumbnail = o.ThumbnailUrl
                    }).ToList()
                }).ToList(),
                Title = result.Title
            };
        }
    }
}
