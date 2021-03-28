namespace Coflnet.SongVoter.DBModels
{
    public class ExternalSong
    {
        public int Id { get; set; }

        /// <summary>
        /// The Platform of this song
        /// </summary>
        public Platforms Platform { get; set; }

        public string Title {get;set;}

        public string Artist {get;set;}

        public string ThumbnailUrl {get;set;}

        public string ExternalId {get;set;}


        /// <summary>
        /// 
        /// </summary>
        public enum Platforms
        {
            Unkown,
            Youtube,
            Spotify
        }
    }


}
