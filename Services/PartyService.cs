using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.SongVoter.Service;
public class PartyService
{
    private readonly SVContext db;

    public PartyService(SVContext db)
    {
        this.db = db;
    }

    public async Task<Party> GetUserParty(User user, bool allowNull = false)
    {
        var parties = await db.Parties.Where(p => p.Creator == user || p.Members.Contains(user))
                        .Include(p => p.Members)
                        .Include(c => c.Creator).FirstOrDefaultAsync();
        if (parties == null && !allowNull)
            throw new ApiException(System.Net.HttpStatusCode.NotFound, "You are not in a party");
        return parties;
    }

    public async Task LeaveParty(User user)
    {
        var party = await GetUserParty(user);
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
                // remove invites
                var invites = await db.Invites.Where(i => i.Party == party).ToListAsync();
                db.RemoveRange(invites);
                db.Remove(party);
                await db.SaveChangesAsync();
            }
        }
        party.Members.Remove(user);
        db.Update(party);
        await db.SaveChangesAsync();
    }
}