using System.Linq;
using Coflnet.SongVoter.Service;

namespace Coflnet.SongVoter.Transformers
{
    public class SongTransformer
    {
        private IDService iDService;

        public SongTransformer(IDService iDService)
        {
            this.iDService = iDService;
        }

        public Models.Song ToApiSong(DBModels.Song db)
        {
            if(db == null)
                return null;
            System.Console.WriteLine($"lookup: {db.Lookup}");
            return new Models.Song()
            {
                Id = iDService.ToHash(db.Id),
                Title = db.Title,
                Occurences = db.ExternalSongs.Select(s => new Models.ExternalSong()
                {
                    Artist = s.Artist,
                    ExternalId = s.ExternalId,
                    Platform = (Models.ExternalSong.PlatformEnum)s.Platform,
                    Thumbnail = s.ThumbnailUrl,
                    Title = s.Title,
                    DurationMs = (int)s.Duration.TotalMilliseconds
                }).ToList()
            };
        }

        public Models.PartyPlaylistEntry ToApiPartyPlaylistEntry(DBModels.PartySong db, int userId)
        {
            if(db == null)
                return null;
            return new Models.PartyPlaylistEntry()
            {
                DownVotes = db.DownVoters.Count(),
                UpVotes = db.UpVoters.Count(),
                SelfVote =  db.UpVoters.Any(u=>u.Id == userId) ? Models.PartyPlaylistEntry.SelfVoteState.Up :
                            db.DownVoters.Any(u=>u.Id == userId)  ? Models.PartyPlaylistEntry.SelfVoteState.Down :
                            Models.PartyPlaylistEntry.SelfVoteState.None,
                Song = ToApiSong(db.Song)
            };
        }
    }
}