using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.Attributes;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Middleware;
using Coflnet.SongVoter.Service;
using Coflnet.SongVoter.Transformers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Coflnet.SongVoter.Controllers
{
    [Route("api/party")]
    public class PartyController : ControllerBase
    {
        private readonly SVContext db;
        private IDService idService;
        private SongTransformer songTransformer;
        private PartyService partyService;
        public PartyController(SVContext data, IDService idService, SongTransformer songTransformer, PartyService partyService)
        {
            this.db = data;
            this.idService = idService;
            this.songTransformer = songTransformer;
            this.partyService = partyService;
        }
        /// <summary>
        /// Creates an invite link for a party
        /// </summary>
        /// <response code="200">invite link created</response>
        [HttpGet]
        [Route("inviteLink")]
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

            return Ok(new Models.Invite(invite, idService));
        }
        /// <summary>
        /// Creates a new party
        /// </summary>
        /// <response code="200">successful created</response>
        [HttpPost]
        [Route("")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("CreateParty")]
        [SwaggerResponse(statusCode: 200, type: typeof(Models.Party), description: "successful created")]
        [SwaggerResponse(statusCode: 409, type: typeof(string), description: "Already in a party")]
        public async Task<ActionResult<Models.Party>> CreateParty(Models.PartyCreateOptions partyCreateOptions)
        {
            var userId = idService.UserId(this);
            Console.WriteLine("user id: " + userId);
            var currentParty = await GetCurrentParty(true);
            if (currentParty != null)
                return Conflict("You are already in a party, leave it first");

            foreach (var item in db.Users.ToList())
            {
                Console.WriteLine("found user: " + Newtonsoft.Json.JsonConvert.SerializeObject(item, Newtonsoft.Json.Formatting.Indented));
            }
            var user = await db.Users.FindAsync(userId) ?? throw new ApiException(System.Net.HttpStatusCode.BadRequest, "User not found " + userId);
            var party = new DBModels.Party()
            {
                Creator = user,
                Name = partyCreateOptions.Name ?? "My party",
                SupportedPlatforms = (Platforms)songTransformer.CombinePlatforms(partyCreateOptions.SupportedPlatforms)
            };
            this.db.Add(party);
            await this.db.SaveChangesAsync();
            await AddUserSongsToParty(party, user);

            return base.Ok(ToExternalParty(party));
        }

        private Models.Party ToExternalParty(Party party)
        {
            return new Models.Party()
            {
                Id = idService.ToHash(party.Id),
                Members = party.Members?.Select(mem => idService.ToHash(mem.Id)).ToList(),
                OwnerId = idService.ToHash(party.Creator.Id),
                SupportedPlatforms = songTransformer.SplitPlatforms(party.SupportedPlatforms),
                Name = party.Name
            };
        }

        private async Task<User> CurrentUser()
        {
            return await db.Users.FindAsync((int)idService.UserId(this));
        }

        private async Task<PartySong> GetOrCreatePartySong(Party party, int sId)
        {
            return (await GetOrCreatePartySongs(party, new List<int>() { sId })).First();
        }

        private async Task<IEnumerable<PartySong>> GetOrCreatePartySongs(Party party, IList<int> songIds)
        {
            var partySongs = await db.PartySongs
                            .Where(ps => ps.PartyId == party.Id && songIds.Contains(ps.SongId))
                            .Include(ps => ps.DownVoters)
                            .Include(ps => ps.UpVoters)
                            .ToListAsync();
            if (partySongs.Count != songIds.Count)
            {
                var missing = songIds.Where(id => !partySongs.Any(ps => ps.SongId == id)).ToList();
                var songs = await db.Songs.Where(s => missing.Contains(s.Id)).Include(s => s.ExternalSongs).ToListAsync();
                foreach (var item in missing)
                {
                    var song = songs.FirstOrDefault(s => s.Id == item) ?? throw new ApiException(System.Net.HttpStatusCode.BadRequest, $"Song with id {idService.ToHash(item)} found");
                    if (!song.ExternalSongs.Any(e => party.SupportedPlatforms.HasFlag(e.Platform)))
                        continue; // unsupported platform for this party
                    partySongs.Add(new PartySong()
                    {
                        PartyId = party.Id,
                        SongId = item
                    });
                }
                db.AddRange(partySongs);
                await db.SaveChangesAsync();
            }
            return partySongs;
        }

        /// <summary>
        /// Returns all parties of the curent user
        /// </summary>
        /// <response code="200">successful created</response>
        [HttpGet]
        [Route("")]
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

        private async Task<Party> GetCurrentParty(bool allowNull = false)
        {
            var user = await CurrentUser();
            return await partyService.GetUserParty(user, allowNull);
        }



        /// <summary>
        /// Invites a user to a party
        /// </summary>
        /// <param name="partyId">ID of party to invite to</param>
        /// <param name="userId">ID of user to invite</param>
        /// <response code="201">invite sent</response>
        [HttpPost]
        [Route("invite/{userId}")]
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
        /// <param name="inviteId">ID of the invite link to join a party with</param>
        /// <response code="200">joined successfully</response>
        [HttpPost]
        [Route("{inviteId}/join")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("JoinParty")]
        [SwaggerResponse(statusCode: 200, description: "joined successfully")]
        [SwaggerResponse(statusCode: 404, type: typeof(string), description: "party not found")]
        public async Task<IActionResult> JoinParty([FromRoute(Name = "inviteId"), Required] string inviteId)
        {
            var invitedId = idService.FromHash(inviteId);
            var party = await db.Invites.Where(i => i.Id == invitedId).Include(i => i.Party).Select(i => i.Party).FirstOrDefaultAsync();
            if (party == null)
                return NotFound("Party not found");
            using var transaction = db.Database.BeginTransaction(IsolationLevel.RepeatableRead);
            try
            {
                var user = await CurrentUser();
                // lock current user 
                await AddUserSongsToParty(party, user);
                var userWithParty = await db.Users.Where(u => u.Id == user.Id).Include(u => u.Parties).FirstAsync();
                userWithParty.Parties.Clear();
                userWithParty.Parties.Add(party);
                await db.SaveChangesAsync();
                return Ok();
            }
            finally
            {
                await transaction.CommitAsync();
            }

        }

        private async Task AddUserSongsToParty(Party party, User user)
        {
            var userId = user.Id;
            var list = await db.PlayLists.Where(pl => pl.Owner == userId).Include(p => p.Songs).FirstOrDefaultAsync();
            if (list == null)
                return; // no songs
            foreach (var item in list.Songs)
            {
                // currently they are never removed
                var song = await GetOrCreatePartySong(party, item.Id);
                song.UpVoters.Add(user);
                //song.DownVoters.Add(user);
                db.Update(song);
            }
        }

        /// <summary>
        /// kicks a user from a party
        /// </summary>
        /// <param name="userId">ID of user to kick</param>
        /// <response code="201">left successfully</response>
        [HttpPost]
        [Route("kick/{userId}")]
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
        [Route("leave")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("LeaveParty")]
        public async Task<IActionResult> LeaveParty()
        {
            var user = await CurrentUser();
            await partyService.LeaveParty(user);
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
        [Route("nextSong")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("NextSong")]
        [SwaggerResponse(statusCode: 200, type: typeof(Models.Song), description: "invite created")]
        public async Task<Models.Song> NextSong()
        {
            var pId = (await GetCurrentParty()).Id;
            var next = await db.PartySongs.Where(ps => ps.Party.Id == pId)
                                .Include(ps => ps.DownVoters)
                                .Include(ps => ps.UpVoters)
                                .Include(ps => ps.Song).ThenInclude(s => s.ExternalSongs)
                                .OrderByDescending(ps => 1 + ps.UpVoters.Count - ps.DownVoters.Count - ps.PlayedTimes)
                                .Select(ps => ps.Song)
                                .FirstOrDefaultAsync();
            return songTransformer.ToApiSong(next);
        }
        [HttpGet]
        [Route("playlist")]
        [Authorize]
        [SwaggerResponse(statusCode: 200, type: typeof(List<Models.PartyPlaylistEntry>), description: "invite created")]
        [SwaggerResponse(statusCode: 404, type: typeof(ApiException), description: "not in a party")]
        public async Task<ActionResult<List<Models.PartyPlaylistEntry>>> GetSongList()
        {
            var pId = (await GetCurrentParty()).Id;
            var list = await db.PartySongs.Where(ps => ps.Party.Id == pId)
                                .Include(ps => ps.DownVoters)
                                .Include(ps => ps.UpVoters)
                                .Include(ps => ps.Song).ThenInclude(s => s.ExternalSongs)
                                .OrderByDescending(ps => 1 + ps.UpVoters.Count - ps.DownVoters.Count - ps.PlayedTimes)
                                .ToListAsync();
            var userId = idService.UserId(this);
            return Ok(list.Select(l => songTransformer.ToApiPartyPlaylistEntry(l, userId)).ToList());
        }

        /// <summary>
        /// Marks a song as played
        /// </summary>
        /// <param name="songId">ID of song to mark</param>
        /// <response code="200">song marked</response>
        /// <response code="404">song not found</response>
        [HttpPost]
        [Route("song/{songId}/played")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("PlayedSong")]
        public async Task<IActionResult> PlayedSong([FromRoute(Name = "songId"), Required] string songId)
        {
            var song = await db.Songs.FindAsync(idService.FromHash(songId));
            if (song == null)
                return NotFound();
            var party = await GetCurrentParty();
            var partySong = await GetOrCreatePartySong(party, song.Id);
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
        [Route("{partyId}/reset")]
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
        /// <param name="songId">ID of the song to upvote</param>
        /// <response code="200">upvoted</response>
        [HttpPost]
        [Route("upvote/{songId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("UpvoteSong")]
        public async Task<IActionResult> UpvoteSong([FromRoute(Name = "songId"), Required] string songId)
        {
            var ps = await GetOrCreatePartySong(songId);
            var user = await CurrentUser();
            ps.DownVoters.Remove(user);
            ps.UpVoters.Add(user);
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        [Route("add")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("AddSongs")]
        public async Task<IActionResult> AddSongs([FromBody, Required] List<string> songIds)
        {
            var currentParty = await GetCurrentParty();
            var user = await CurrentUser();
            var songs = await GetOrCreatePartySongs(currentParty, songIds.Select(idService.FromHash).ToList());
            foreach (var song in songs)
            {
                if (!song.UpVoters.Contains(user))
                    song.UpVoters.Add(user);
            }
            await db.SaveChangesAsync();
            return Ok();
        }

        private async Task<PartySong> GetOrCreatePartySong(string songId)
        {
            var currentParty = await GetCurrentParty();
            var ps = await GetOrCreatePartySong(currentParty, (int)idService.FromHash(songId));
            return ps;
        }

        /// <summary>
        /// votes a song down so it is play later/not at all
        /// </summary>
        /// <param name="songId">ID of the song</param>
        /// <response code="200">downvote accepted</response>
        [HttpPost]
        [Route("downvote/{songId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("DownvoteSong")]
        public async Task<IActionResult> DownvoteSong([FromRoute(Name = "songId"), Required] string songId)
        {
            var ps = await GetOrCreatePartySong(songId);
            var user = await CurrentUser();
            ps.DownVoters.Add(user);
            ps.UpVoters.Remove(user);
            await db.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Remove vote from song
        /// </summary>
        /// <param name="songId">ID of the song</param>
        /// <response code="200">vote removed</response>
        [HttpPost]
        [Route("removeVote/{songId}")]
        [Authorize]
        [ValidateModelState]
        [SwaggerOperation("RemoveVote")]
        public async Task<IActionResult> RemoveVote([FromRoute(Name = "songId"), Required] string songId)
        {
            var ps = await GetOrCreatePartySong(songId);
            var user = await CurrentUser();
            ps.DownVoters.Remove(user);
            ps.UpVoters.Remove(user);
            await db.SaveChangesAsync();
            return Ok();
        }
    }
}
