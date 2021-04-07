using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Models;
using Coflnet.SongVoter.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.SongVoter.Controllers.Impl
{
    public class ListApiControllerImpl : ListApiController
    {
        private SVContext db;
        private IDService iDService;

        public ListApiControllerImpl(SVContext db)
        {
            this.db = db;
            iDService = IDService.Instance;
        }


        public override async Task<IActionResult> CreatePlaylist([FromBody] PlayList playList)
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
            return iDService.FromHash(this.User.Claims.Where(c => c.Type == "uid").First().Value);
        }

        public override async Task<IActionResult> GetListById([FromRoute(Name = "listId"), Required] string listId)
        {
            var dbId = iDService.FromHash(listId);
            var userId = GetUserId();
            var result = await db.PlayLists.Where(p => p.Id == dbId && p.Owner == userId).Include(p => p.Songs).FirstOrDefaultAsync();
            return base.Ok(DBToApiPlaylist(result));
        }



        public override async Task<IActionResult> GetPlaylists()
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
