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
                    Title = s.Title
                }).ToList()
            };
        }
    }
}