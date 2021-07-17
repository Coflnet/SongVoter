using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace Coflnet.SongVoter.DBModels
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string GoogleId { get; set; }

        public ICollection<PartySong> Upvotes {get;set;}
        public ICollection<PartySong> Downvotes {get;set;}
        public ICollection<Party> Parties {get;set;}

        public override bool Equals(object obj)
        {
            return obj is User user &&
                   Id == user.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, GoogleId);
        }
    }
}
