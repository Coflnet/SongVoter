using System.Collections.Generic;

namespace Coflnet.SongVoter.DBModels
{
    public class Song
    {
        public int Id { get; set; }
        /// <summary>
        /// The title/name of the song
        /// </summary>
        /// <value></value>
        public string Title { get; set; }

        public List<ExternalSong> ExternalSongs { get; set; }

    }
}
