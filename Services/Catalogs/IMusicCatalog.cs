using System.Collections.Generic;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;

namespace Coflnet.SongVoter.Service;

// A provider resolves metadata only. Playback belongs to the host's native SDK adapter.
public interface IMusicCatalog
{
    Platforms Platform { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<ExternalSong>> Search(string term);
    Task<ExternalSong> Resolve(string externalId);
}
