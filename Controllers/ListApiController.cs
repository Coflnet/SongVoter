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
        [Route("/v1/lists")]
        [Authorize]
        [Consumes("application/json")]
        [ValidateModelState]
        [SwaggerOperation("CreatePlaylist")]
        [SwaggerResponse(statusCode: 200, type: typeof(PlayList), description: "successful created list")]
        public async Task<IActionResult> CreatePlaylist([FromBody] PlayList playList)
        {
            var userId = GetUserId();
            var songIds = playList.Songs.Select(sid => iDService.FromHash(sid));
            db.Add(new DBModels.Playlist()
            {
                Owner = (int)userId,
                Title = playList.Title,
                Songs = db.Songs.Where(s => songIds.Contains(s.Id)).ToList()
            });
            await db.SaveChangesAsync();
            return Ok();
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
        [Route("/v1/lists/{listId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("GetListById")]
        [SwaggerResponse(statusCode: 200, type: typeof(PlayList), description: "successful operation")]
        public async Task<IActionResult> GetListById([FromRoute(Name = "listId"), Required] string listId)
        {
            var dbId = iDService.FromHash(listId);
            var userId = GetUserId();
            var result = await db.PlayLists.Where(p => p.Id == dbId && p.Owner == userId).Include(p => p.Songs).FirstOrDefaultAsync();
            return base.Ok(DBToApiPlaylist(result));
        }



        /// <summary>
        /// Get playlist for active user
        /// </summary>
        /// <response code="200">successful response</response>
        [HttpGet]
        [Route("/v1/lists")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("GetPlaylists")]
        [SwaggerResponse(statusCode: 200, type: typeof(List<PlayList>), description: "successful response")]
        public async Task<IActionResult> GetPlaylists()
        {
            var userId = GetUserId();
            var result = await db.PlayLists.Where(p =>p.Owner == userId).Include(p => p.Songs).ToListAsync();
            return Ok(result.Select(p=>DBToApiPlaylist(p)));
        }

        private PlayList DBToApiPlaylist(Playlist result)
        {
            return new Models.PlayList()
            {
                Id = iDService.ToHash(result.Id),
                Songs = result.Songs.Select(s => iDService.ToHash(s.Id)).ToList(),
                Title = result.Title
            };
        }
    }
}
