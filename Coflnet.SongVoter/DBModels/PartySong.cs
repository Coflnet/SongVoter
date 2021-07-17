using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coflnet.SongVoter.DBModels
{
    /// <summary>
    /// Maps a party to songs
    /// </summary>
    public class PartySong
    {
        public long Id { get; set; }
        public Party Party { get; set; }
        [ForeignKey("Party")]
        public int PartyId { get; set; }
        /// <summary>
        /// Who (and how many) upvoted this
        /// </summary>
        public ICollection<User> UpVoters { get; set; } = new HashSet<User>();
        /// <summary>
        /// Who (and how many) downvoted this
        /// </summary>
        public ICollection<User> DownVoters { get; set; } = new HashSet<User>();
        /// <summary>
        /// How often this song has been played already
        /// </summary>
        public short PlayedTimes { get; set; }
        public Song Song { get; set; }
        [ForeignKey("Song")]
        public int SongId { get; set; }
    }
}
