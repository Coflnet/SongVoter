using System;

namespace Coflnet.SongVoter.DBModels
{
    public class Invite
    {
        public int Id { get; set; }
        public Party Party { get; set; }
        public User User { get; set; }
        public int CreatorId { get; set; }
        public DateTime ValidUntil { get; set; }
    }
}
