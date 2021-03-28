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
    public abstract class PartyApiController : ControllerBase
    { 
        /// <summary>
        /// Creates an invite link for a party
        /// </summary>
        /// <param name="partyId">ID of party to invite to</param>
        /// <response code="200">invite created</response>
        [HttpGet]
        [Route("/v1/party/{partyId}/inviteLink")]
        [ValidateModelState]
        [SwaggerOperation("CreateInviteLink")]
        public abstract IActionResult CreateInviteLink([FromRoute (Name = "partyId")][Required]string partyId);

        /// <summary>
        /// Creates a new party
        /// </summary>
        /// <response code="200">successful created</response>
        [HttpPost]
        [Route("/v1/partys")]
        [ValidateModelState]
        [SwaggerOperation("CreateParty")]
        [SwaggerResponse(statusCode: 200, type: typeof(Party), description: "successful created")]
        public abstract IActionResult CreateParty();

        /// <summary>
        /// votes a song down so it is play later/not at all
        /// </summary>
        /// <param name="partyId">ID of party</param>
        /// <param name="songId">ID of the song</param>
        /// <response code="200">downvote accepted</response>
        [HttpPost]
        [Route("/v1/party/{partyId}/downvote/{songId}")]
        [ValidateModelState]
        [SwaggerOperation("DownvoteSong")]
        public abstract IActionResult DownvoteSong([FromRoute (Name = "partyId")][Required]string partyId, [FromRoute (Name = "songId")][Required]int songId);

        /// <summary>
        /// Returns all parties of the curent user
        /// </summary>
        /// <response code="200">successful created</response>
        [HttpGet]
        [Route("/v1/partys")]
        [ValidateModelState]
        [SwaggerOperation("GetParties")]
        [SwaggerResponse(statusCode: 200, type: typeof(List<Party>), description: "successful created")]
        public abstract IActionResult GetParties();

        /// <summary>
        /// Invites a user to a party
        /// </summary>
        /// <param name="partyId">ID of party to invite to</param>
        /// <param name="userId">ID of user to invite</param>
        /// <response code="201">invite sent</response>
        [HttpPost]
        [Route("/v1/party/{partyId}/invite/{userId}")]
        [ValidateModelState]
        [SwaggerOperation("InviteToParty")]
        public abstract IActionResult InviteToParty([FromRoute (Name = "partyId")][Required]string partyId, [FromRoute (Name = "userId")][Required]string userId);

        /// <summary>
        /// Joins a party
        /// </summary>
        /// <param name="partyId">ID of party to join</param>
        /// <response code="201">joined successfully</response>
        [HttpPost]
        [Route("/v1/party/{partyId}/join")]
        [ValidateModelState]
        [SwaggerOperation("JoinParty")]
        public abstract IActionResult JoinParty([FromRoute (Name = "partyId")][Required]string partyId);

        /// <summary>
        /// kicks a user from a party
        /// </summary>
        /// <param name="partyId">ID of party to leave</param>
        /// <param name="userId">ID of user to kick</param>
        /// <response code="201">left successfully</response>
        [HttpPost]
        [Route("/v1/party/{partyId}/kick/{userId}")]
        [ValidateModelState]
        [SwaggerOperation("KickFromParty")]
        public abstract IActionResult KickFromParty([FromRoute (Name = "partyId")][Required]string partyId, [FromRoute (Name = "userId")][Required]string userId);

        /// <summary>
        /// Leave a party
        /// </summary>
        /// <param name="partyId">ID of party to leave</param>
        /// <response code="201">left successfully</response>
        [HttpPost]
        [Route("/v1/party/{partyId}/leave")]
        [ValidateModelState]
        [SwaggerOperation("LeaveParty")]
        public abstract IActionResult LeaveParty([FromRoute (Name = "partyId")][Required]string partyId);

        /// <summary>
        /// gets the next Song
        /// </summary>
        /// <param name="partyId">ID of party</param>
        /// <response code="200">invite created</response>
        [HttpGet]
        [Route("/v1/party/{partyId}/nextSong")]
        [ValidateModelState]
        [SwaggerOperation("NextSong")]
        [SwaggerResponse(statusCode: 200, type: typeof(Song), description: "invite created")]
        public abstract IActionResult NextSong([FromRoute (Name = "partyId")][Required]string partyId);

        /// <summary>
        /// resets the parties playing state
        /// </summary>
        /// <param name="partyId">ID of party to invite to</param>
        /// <response code="200">reset party</response>
        [HttpPost]
        [Route("/v1/party/{partyId}/reset")]
        [ValidateModelState]
        [SwaggerOperation("ResetParty")]
        public abstract IActionResult ResetParty([FromRoute (Name = "partyId")][Required]string partyId);

        /// <summary>
        /// votes a song up so it is play sooner
        /// </summary>
        /// <remarks>Adds an upvote to an song wich causes it to be played sooner. Also adds new songs to a party</remarks>
        /// <param name="partyId">ID of party</param>
        /// <param name="songId">ID of the song to upvote</param>
        /// <response code="200">upvoted</response>
        [HttpPost]
        [Route("/v1/party/{partyId}/upvote/{songId}")]
        [ValidateModelState]
        [SwaggerOperation("UpvoteSong")]
        public abstract IActionResult UpvoteSong([FromRoute (Name = "partyId")][Required]string partyId, [FromRoute (Name = "songId")][Required]int songId);
    }
}
