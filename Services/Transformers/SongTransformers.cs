using System;
using System.Collections.Generic;
using System.Linq;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Models;
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
            if (db == null)
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
                    Platform = (SongPlatform)s.Platform,
                    Thumbnail = s.ThumbnailUrl,
                    Title = s.Title,
                    DurationMs = (int)s.Duration.TotalMilliseconds
                }).ToList()
            };
        }

        public Models.PartyPlaylistEntry ToApiPartyPlaylistEntry(DBModels.PartySong db, int userId)
        {
            if (db == null)
                return null;
            return new Models.PartyPlaylistEntry()
            {
                DownVotes = db.DownVoters.Count(),
                UpVotes = db.UpVoters.Count(),
                SelfVote = db.UpVoters.Any(u => u.Id == userId) ? Models.PartyPlaylistEntry.SelfVoteState.Up :
                            db.DownVoters.Any(u => u.Id == userId) ? Models.PartyPlaylistEntry.SelfVoteState.Down :
                            Models.PartyPlaylistEntry.SelfVoteState.None,
                Song = ToApiSong(db.Song)
            };
        }

        public SongPlatform CombinePlatforms(SongPlatform[] platforms)
        {
            if (platforms.Length == 0)
                return SongPlatform.Youtube | SongPlatform.Spotify;
            platforms = platforms ?? new SongPlatform[] { SongPlatform.Spotify, SongPlatform.Youtube };
            SongPlatform combinedPlatforms = platforms.Aggregate((a, b) => a | b);
            return combinedPlatforms;
        }

        internal SongPlatform[] SplitPlatforms(Platforms supportedPlatforms)
        {
            var platforms = (SongPlatform)supportedPlatforms;
            var result = new List<SongPlatform>();
            foreach (SongPlatform platform in Enum.GetValues(typeof(SongPlatform)))
            {
                if (platforms.HasFlag(platform))
                    result.Add(platform);
            }
            return result.ToArray();
        }
    }
}