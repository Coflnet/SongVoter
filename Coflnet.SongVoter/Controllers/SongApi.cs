/*
 * Songvoter
 *
 * Definition for songvoter API
 *
 * The version of the OpenAPI document: 0.0.1
 * Contact: support@coflnet.com
 * Generated by: https://openapi-generator.tech
 */

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;
using Newtonsoft.Json;
using Coflnet.SongVoter.Attributes;
using Coflnet.SongVoter.Models;

namespace Coflnet.SongVoter.Controllers
{ 
    /// <summary>
    /// 
    /// </summary>
    [ApiController]
    public abstract class SongApiController : ControllerBase
    { 
        /// <summary>
        /// Add a new song by url
        /// </summary>
        /// <param name="songCreation">Pet object that needs to be added to the store</param>
        /// <response code="405">Invalid input</response>
        [HttpPost]
        [Route("/v1/songs")]
        [Authorize]
        [Consumes("application/json")]
        [ValidateModelState]
        [SwaggerOperation("AddSong")]
        public abstract Task<IActionResult> AddSong([FromBody]SongCreation songCreation);

        /// <summary>
        /// Finds Song by search term
        /// </summary>
        /// <param name="term">Search term to serach for</param>
        /// <response code="200">successful operation</response>
        /// <response code="400">Invalid search term</response>
        [HttpGet]
        [Route("/v1/songs/search")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("FindSong")]
        [SwaggerResponse(statusCode: 200, type: typeof(List<Song>), description: "successful operation")]
        public abstract Task<IActionResult> FindSong([FromQuery (Name = "term")][Required()]string term);

        /// <summary>
        /// Find song by ID
        /// </summary>
        /// <remarks>Returns a single song</remarks>
        /// <param name="songId">ID of song to return</param>
        /// <response code="200">successful operation</response>
        /// <response code="400">Invalid ID supplied</response>
        /// <response code="404">Song not found</response>
        [HttpGet]
        [Route("/v1/song/{songId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("GetSongById")]
        [SwaggerResponse(statusCode: 200, type: typeof(Song), description: "successful operation")]
        public abstract Task<IActionResult> GetSongById([FromRoute (Name = "songId")][Required]string songId);
    }
}
