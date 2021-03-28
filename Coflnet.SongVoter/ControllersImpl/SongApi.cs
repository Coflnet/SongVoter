using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Coflnet.SongVoter.Models;
using Coflnet.SongVoter.DBModels;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.SongVoter.Controllers.Impl
{
    public class SongApiControllerImpl : SongApiController
    {
        private readonly SVContext db;
        public SongApiControllerImpl(SVContext data)
        {
            this.db = data;
        }

        public override Task<IActionResult> AddSong([FromBody] SongCreation body)
        {
            
            throw new NotImplementedException();
        }

        public override async Task<IActionResult> FindSong([FromQuery(Name = "term"), Required] string term)
        {
            //this.User.RequireScope(Scope.Song);

            var db = await this.db
                .Songs.Where(s=>s.Id == 1).FirstOrDefaultAsync();

            return Ok(new Models.Song(){
                Id = db.Id,
                Title = db.Title
            });
        }

        public override Task<IActionResult> GetSongById([FromRoute(Name = "songId"), Required] long songId)
        {
            throw new NotImplementedException();
        }
    }
}
