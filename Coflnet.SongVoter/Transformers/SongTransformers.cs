using System.Linq;
using Coflnet.SongVoter.Service;

namespace Coflnet.SongVoter.Transformers
{
    public static class SongTransformer
    {
         public static Models.Song ToApiSong(this DBModels.Song db)
        {
            return new Models.Song()
            {
                Id = IDService.Instance.ToHash(db.Id),
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