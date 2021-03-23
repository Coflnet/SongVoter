using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Coflnet.SongVoter.Models;

namespace Coflnet.SongVoter.Controllers.Impl
{
    public class SongApiControllerImpl : SongApiController
    {
        public override IActionResult AddSong([FromBody] SongCreation body)
        {
            throw new NotImplementedException();
        }

        public override IActionResult FindSong([FromQuery(Name = "term"), Required] string term)
        {
            throw new NotImplementedException();
        }

        public override IActionResult GetSongById([FromRoute(Name = "songId"), Required] long songId)
        {
            throw new NotImplementedException();
        }
    }
}
