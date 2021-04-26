using System.Collections.Generic;

namespace Coflnet.SongVoter.DBModels
{
    /// <summary>
    /// Maps a party to songs
    /// </summary>
    public class PartySong
    {
        public long Id { get; set; }
        public Party Party { get; set; }
        public int PartyId { get; set; }
        /// <summary>
        /// Who (and how many) upvoted this
        /// </summary>
        public HashSet<User> UpVoters { get; set; }
        /// <summary>
        /// Who (and how many) downvoted this
        /// </summary>
        public HashSet<User> DownVoters { get; set; }
        /// <summary>
        /// How often this song has been played already
        /// </summary>
        public short PlayedTimes { get; set; }
        public Song Song { get; set; }
        public int SongId { get; set; }
    }
}
