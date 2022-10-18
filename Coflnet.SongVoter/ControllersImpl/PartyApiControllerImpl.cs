using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Coflnet.SongVoter.Transformers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.SongVoter.Controllers.Impl
{
    public class PartyApiControllerImpl : PartyApiController
    {
        private readonly SVContext db;
        private IDService idService;
        private SongTransformer songTransformer;
        public PartyApiControllerImpl(SVContext data, IDService idService, SongTransformer songTransformer)
        {
            this.db = data;
            this.idService = idService;
            this.songTransformer = songTransformer;
        }

        public override async Task<IActionResult> CreateInviteLink([FromRoute(Name = "partyId"), Required] string partyId)
        {
            var invite = new Invite()
            {
                CreatorId = (int)idService.UserId(this),
                Party = await db.Parties.FindAsync(idService.FromHash(partyId)),
                ValidUntil = DateTime.Now.AddDays(1)
            };
            db.Add(invite);
            await db.SaveChangesAsync();

            return Ok($"https://songvoter.party/invite/idService.ToHash(invite.Id)");
        }

        public override async Task<IActionResult> CreateParty()
        {
            var userId = idService.UserId(this);

            var party = new DBModels.Party()
            {
                Creator = db.Users.Find(userId),
                Name = "new party"
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
                Members = party.Members.Select(mem => idService.ToHash(mem.Id)).ToList(),
                Name = party.Name
            };
        }

        public override async Task<IActionResult> DownvoteSong([FromRoute(Name = "partyId"), Required] string partyId, [FromRoute(Name = "songId"), Required] string songId)
        {
            var ps = await GetOrCreatePartySong(partyId, songId);
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

        public override async Task<IActionResult> GetParties()
        {
            var user = await CurrentUser();
            var parties = db.Parties.Where(p => p.Creator == user || p.Members.Contains(user));
            return Ok(parties.Select(ToExternalParty));
        }

        public override Task<IActionResult> InviteToParty([FromRoute(Name = "partyId"), Required] string partyId, [FromRoute(Name = "userId"), Required] string userId)
        {
            throw new NotImplementedException("not implemented because there is no way of checking invites");
        }

        public override async Task<IActionResult> JoinParty([FromRoute(Name = "partyId"), Required] string partyId)
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
            await db.SaveChangesAsync();
            return Ok();
        }

        public override async Task<IActionResult> KickFromParty([FromRoute(Name = "partyId"), Required] string partyId, [FromRoute(Name = "userId"), Required] string userId)
        {
            var user = await db.Users.FindAsync(idService.FromHash(userId));
            await RemoveUserFromParty(partyId, user);
            return Ok();
        }

        public override async Task<IActionResult> LeaveParty([FromRoute(Name = "partyId"), Required] string partyId)
        {
            var user = await CurrentUser();
            await RemoveUserFromParty(partyId, user);
            return Ok();
        }

        private async Task RemoveUserFromParty(string partyId, User user)
        {
            Party party = await GetParty(partyId);
            party.Members.Remove(user);
            db.Update(party);
            await db.SaveChangesAsync();
        }

        private async Task<Party> GetParty(string partyId)
        {
            var pId = idService.FromHash(partyId);
            var party = await db.Parties.Where(p => p.Id == pId).Include(p => p.Members).FirstOrDefaultAsync();
            return party;
        }

        public override async Task<IActionResult> NextSong([FromRoute(Name = "partyId"), Required] string partyId)
        {
            var pId = idService.FromHash(partyId);
            var next = await db.PartySongs.Where(ps => ps.Id == pId)
                                .Include(ps => ps.DownVoters)
                                .Include(ps => ps.UpVoters)
                                .OrderByDescending(ps => 1 + ps.UpVoters.Count - ps.DownVoters.Count - ps.PlayedTimes)
                                .Select(ps => ps.Song)
                                .FirstOrDefaultAsync();
            return Ok(songTransformer.ToApiSong(next));
        }

        public override async Task<IActionResult> ResetParty([FromRoute(Name = "partyId"), Required] string partyId)
        {
            var pId = idService.FromHash(partyId);
            foreach (var item in db.PartySongs.Where(ps => ps.Id == pId))
            {
                item.PlayedTimes = 0;
            }
            await db.SaveChangesAsync();
            return Ok();
        }

        public override async Task<IActionResult> UpvoteSong([FromRoute(Name = "partyId"), Required] string partyId, [FromRoute(Name = "songId"), Required] string songId)
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
