using System.Collections.Generic;

namespace Coflnet.SongVoter.DBModels
{
    public class Playlist
    {
        /// <summary>
        /// Gets or Sets Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// The id of the user owning this Playlist
        /// </summary>
        public int Owner { get; set; }

        /// <summary>
        /// Gets or Sets Title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or Sets Songs
        /// </summary>
        public ICollection<Song> Songs { get; set; }
    }
}
