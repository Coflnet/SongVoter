using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.Attributes;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Coflnet.SongVoter.Transformers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Coflnet.SongVoter.Controllers
{
    public class PartyController : ControllerBase
    {
        private readonly SVContext db;
        private IDService idService;
        private SongTransformer songTransformer;
        public PartyController(SVContext data, IDService idService, SongTransformer songTransformer)
        {
            this.db = data;
            this.idService = idService;
            this.songTransformer = songTransformer;
        }
        /// <summary>
        /// Creates an invite link for a party
        /// </summary>
        /// <response code="200">invite link created</response>
        [HttpGet]
        [Route("/party/inviteLink")]
        //[Authorize]
        [SwaggerOperation("CreateInviteLink")]
        public async Task<ActionResult<Models.Invite>> CreateInviteLink()
        {
            var party = await GetCurrentParty();
            var userId = idService.UserId(this);
            var existing = await db.Invites.Where(i => i.Party == party && i.CreatorId == userId && i.ValidUntil > DateTime.UtcNow).FirstOrDefaultAsync();
            if (existing != null)
                return Ok(new Models.Invite(existing, idService));
            var invite = new Invite()
            {
                CreatorId = userId,
                Party = party,
                ValidUntil = DateTime.UtcNow.AddDays(1)
            };
            db.Add(invite);
            await db.SaveChangesAsync();

            return Ok(new Models.Invite(existing, idService));
        }
        /// <summary>
        /// Creates a new party
        /// </summary>
        /// <response code="200">successful created</response>
        [HttpPost]
        [Route("/party")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("CreateParty")]
        [SwaggerResponse(statusCode: 200, type: typeof(Party), description: "successful created")]
        [SwaggerResponse(statusCode: 400, type: typeof(string), description: "invite link created")]
        public async Task<IActionResult> CreateParty(Models.PartyCreateOptions partyCreateOptions)
        {
            var userId = idService.UserId(this);
            var currentParty = await GetCurrentParty();
            if (currentParty != null)
                return BadRequest("You are already in a party, leave it first");

            var party = new DBModels.Party()
            {
                Creator = db.Users.Find(userId),
                Name = partyCreateOptions.Name ?? "My party"
            };
            this.db.Add(party);
            await this.db.SaveChangesAsync();

            return base.Ok(ToExternalParty(party));
        }

        private Models.Party ToExternalParty(Party party)
        {
            return new Models.Party()
            {
                Id = idService.ToHash(party.Id),
                Members = party.Members?.Select(mem => idService.ToHash(mem.Id)).ToList(),
                OwnerId = idService.ToHash(party.Creator.Id),
                Name = party.Name
            };
        }
        /// <summary>
        /// votes a song down so it is play later/not at all
        /// </summary>
        /// <param name="songId">ID of the song</param>
        /// <response code="200">downvote accepted</response>
        [HttpPost]
        [Route("/party/downvote/{songId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("DownvoteSong")]
        public async Task<IActionResult> DownvoteSong([FromRoute(Name = "songId"), Required] string songId)
        {
            var currentParty = await GetCurrentParty();
            var ps = await GetOrCreatePartySong(currentParty.Id, (int)idService.FromHash(songId));
            var user = await CurrentUser();
            ps.DownVoters.Add(user);
            ps.UpVoters.Remove(user);
            await db.SaveChangesAsync();
            return Ok();
        }

        private async Task<User> CurrentUser()
        {
            return await db.Users.FindAsync((int)idService.UserId(this));
        }

        private async Task<PartySong> GetOrCreatePartySong(string partyId, string songId)
        {
            var pId = (int)idService.FromHash(partyId);
            var sId = (int)idService.FromHash(songId);
            return await GetOrCreatePartySong(pId, sId);
        }

        private async Task<PartySong> GetOrCreatePartySong(int pId, int sId)
        {
            var partySong = await db.PartySongs
                            .Where(ps => ps.PartyId == pId && ps.SongId == sId)
                            .Include(ps => ps.DownVoters)
                            .Include(ps => ps.UpVoters)
                            .FirstOrDefaultAsync();
            if (partySong == null)
            {
                partySong = new PartySong()
                {
                    PartyId = pId,
                    SongId = sId
                };
                db.Add(partySong);
                await db.SaveChangesAsync();
            }
            return partySong;
        }
        /// <summary>
        /// Returns all parties of the curent user
        /// </summary>
        /// <response code="200">successful created</response>
        [HttpGet]
        [Route("/party")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("GetParties")]
        [SwaggerResponse(statusCode: 200, type: typeof(Models.Party), description: "current party")]
        [SwaggerResponse(statusCode: 404, type: typeof(Models.Party), description: "party")]
        public async Task<ActionResult<Models.Party>> GetParty()
        {
            var party = await GetCurrentParty();
            if (party == null)
                return NotFound();
            return Ok(ToExternalParty(party));
        }

        private async Task<Party> GetCurrentParty()
        {
            var user = await CurrentUser();
            var parties = await db.Parties.Where(p => p.Creator == user || p.Members.Contains(user))
                .Include(p => p.Members)
                .Include(c => c.Creator).FirstOrDefaultAsync();
            return parties;
        }

        /// <summary>
        /// Invites a user to a party
        /// </summary>
        /// <param name="partyId">ID of party to invite to</param>
        /// <param name="userId">ID of user to invite</param>
        /// <response code="201">invite sent</response>
        [HttpPost]
        [Route("/party/invite/{userId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("InviteToParty")]
        public Task<IActionResult> InviteToParty([FromRoute(Name = "partyId"), Required] string partyId, [FromRoute(Name = "userId"), Required] string userId)
        {
            throw new NotImplementedException("not implemented because there is no way of checking invites");
        }

        /// <summary>
        /// Joins a party
        /// </summary>
        /// <param name="partyId">ID of party to join</param>
        /// <response code="201">joined successfully</response>
        [HttpPost]
        [Route("/party/{partyId}/join")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("JoinParty")]
        public async Task<IActionResult> JoinParty([FromRoute(Name = "partyId"), Required] string partyId)
        {
            var party = await GetParty(partyId);
            var user = await CurrentUser();
            var list = await db.PlayLists.Where(pl => pl.Owner == user.Id).FirstAsync();
            foreach (var item in list.Songs)
            {
                // currently they are never removed
                var song = await GetOrCreatePartySong(party.Id, item.Id);
                song.UpVoters.Add(user);
                song.DownVoters.Add(user);
                db.Update(song);
            }
            var userWithParty = await db.Users.Where(u => u.Id == user.Id).Include(u => u.Parties).FirstAsync();
            userWithParty.Parties.Clear();
            userWithParty.Parties.Add(party);
            user.Parties.Add(party);
            await db.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// kicks a user from a party
        /// </summary>
        /// <param name="userId">ID of user to kick</param>
        /// <response code="201">left successfully</response>
        [HttpPost]
        [Route("/party/kick/{userId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("KickFromParty")]
        public async Task<IActionResult> KickFromParty([FromRoute(Name = "userId"), Required] string userId)
        {
            var user = await db.Users.FindAsync(idService.FromHash(userId));

            var party = await GetCurrentParty();
            if (party.Creator != await CurrentUser())
                return BadRequest("only creator can kick");
            party.Members.Remove(user);
            db.Update(party);
            await db.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Leave a party
        /// </summary>
        /// <response code="201">left successfully</response>
        [HttpPost]
        [Route("/party/leave")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("LeaveParty")]
        public async Task<IActionResult> LeaveParty()
        {
            var user = await CurrentUser();
            var party = await GetCurrentParty();
            if (party == null)
                return Ok("no party to leave");
            if (party.Creator == user)
            {
                // transfer ownership to other member if possible
                if (party.Members.Count > 0)
                {
                    party.Creator = party.Members.First();
                    party.Members.Remove(party.Creator);
                    db.Update(party);
                    await db.SaveChangesAsync();
                    Console.WriteLine("transfared party");
                }
                else
                {
                    db.Remove(party);
                    await db.SaveChangesAsync();
                }
                return Ok("transfared/deleted party");
            }
            party.Members.Remove(user);
            db.Update(party);
            await db.SaveChangesAsync();
            return Ok();
        }

        private async Task<Party> GetParty(string partyId)
        {
            var pId = idService.FromHash(partyId);
            var party = await db.Parties.Where(p => p.Id == pId).Include(p => p.Members).FirstOrDefaultAsync();
            return party;
        }

        /// <summary>
        /// gets the next Song
        /// </summary>
        /// <param name="partyId">ID of party</param>
        /// <response code="200">invite created</response>
        [HttpGet]
        [Route("/party/nextSong")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("NextSong")]
        [SwaggerResponse(statusCode: 200, type: typeof(Song), description: "invite created")]
        public async Task<IActionResult> NextSong()
        {
            var pId = (await GetCurrentParty()).Id;
            var next = await db.PartySongs.Where(ps => ps.Party.Id == pId)
                                .Include(ps => ps.DownVoters)
                                .Include(ps => ps.UpVoters)
                                .OrderByDescending(ps => 1 + ps.UpVoters.Count - ps.DownVoters.Count - ps.PlayedTimes)
                                .Select(ps => ps.Song)
                                .FirstOrDefaultAsync();
            return Ok(songTransformer.ToApiSong(next));
        }

        /// <summary>
        /// Marks a song as played
        /// </summary>
        /// <param name="songId">ID of song to mark</param>
        /// <response code="200">song marked</response>
        /// <response code="404">song not found</response>
        [HttpPost]
        [Route("/party/song/{songId}/played")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("PlayedSong")]
        public async Task<IActionResult> PlayedSong([FromRoute(Name = "songId"), Required] string songId)
        {
            var song = await db.Songs.FindAsync(idService.FromHash(songId));
            if (song == null)
                return NotFound();
            var party = await GetCurrentParty();
            var partySong = await GetOrCreatePartySong(party.Id, song.Id);
            partySong.PlayedTimes++;
            db.Update(partySong);
            await db.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// resets the parties playing state
        /// </summary>
        /// <param name="partyId">ID of party to invite to</param>
        /// <response code="200">reset party</response>
        [HttpPost]
        [Route("/party/{partyId}/reset")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("ResetParty")]
        public async Task<IActionResult> ResetParty([FromRoute(Name = "partyId"), Required] string partyId)
        {
            var pId = idService.FromHash(partyId);
            foreach (var item in db.PartySongs.Where(ps => ps.Id == pId))
            {
                item.PlayedTimes = 0;
            }
            await db.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// votes a song up so it is play sooner
        /// </summary>
        /// <remarks>Adds an upvote to an song wich causes it to be played sooner. Also adds new songs to a party</remarks>
        /// <param name="partyId">ID of party</param>
        /// <param name="songId">ID of the song to upvote</param>
        /// <response code="200">upvoted</response>
        [HttpPost]
        [Route("/party/{partyId}/upvote/{songId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("UpvoteSong")]
        public async Task<IActionResult> UpvoteSong([FromRoute(Name = "partyId"), Required] string partyId, [FromRoute(Name = "songId"), Required] string songId)
        {
            var ps = await GetOrCreatePartySong(partyId, songId);
            var user = await CurrentUser();
            ps.DownVoters.Remove(user);
            ps.UpVoters.Add(user);
            await db.SaveChangesAsync();
            return Ok();
        }
    }
}