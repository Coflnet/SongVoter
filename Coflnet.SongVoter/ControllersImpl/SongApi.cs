using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Coflnet.SongVoter.Models;
using Coflnet.SongVoter.DBModels;
using System.Linq;

namespace Coflnet.SongVoter.Controllers.Impl
{
    public class SongApiControllerImpl : SongApiController
    {
        private readonly SVContext data;
        public SongApiControllerImpl(SVContext data)
        {
            this.data = data;
        }

        public override IActionResult AddSong([FromBody] SongCreation body)
        {
            throw new NotImplementedException();
        }

        public override IActionResult FindSong([FromQuery(Name = "term"), Required] string term)
        {
            var db = this.data
                .Songs.Where(s=>s.Id == 1).FirstOrDefault();

            return Ok(new Models.Song(){
                Id = db.Id,
                Title = db.Title
            });
        }

        public override IActionResult GetSongById([FromRoute(Name = "songId"), Required] long songId)
        {
            throw new NotImplementedException();
        }
    }
}
