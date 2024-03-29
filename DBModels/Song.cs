using System;
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
        /// <summary>
        /// Combined text for full text song search, max 200 characters
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(200)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "varchar(200)")]
        public string Lookup { get; set; }

        public List<ExternalSong> ExternalSongs { get; set; }
        /// <summary>
        /// Plalists containing this song
        /// here to autogenerate many-to-many table
        /// </summary>
        public ICollection<Playlist> Playlists { get; set; }

        public Song()
        {
            ExternalSongs = new List<ExternalSong>();
        }

        public override bool Equals(object obj)
        {
            return obj is Song song &&
                   Id == song.Id &&
                   Title == song.Title &&
                   Lookup == song.Lookup;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Title, Lookup);
        }
    }
}
