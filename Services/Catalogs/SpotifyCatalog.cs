using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;

namespace Coflnet.SongVoter.Service;

public class SpotifyCatalog(IConfiguration configuration) : IMusicCatalog
{
    public Platforms Platform => Platforms.Spotify;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["spotify:clientid"]) && !string.IsNullOrWhiteSpace(configuration["spotify:clientsecret"]);
    private SpotifyClient Client() => new(SpotifyClientConfig.CreateDefault().WithAuthenticator(
        new ClientCredentialsAuthenticator(configuration["spotify:clientid"], configuration["spotify:clientsecret"])));
    public async Task<ExternalSong> Resolve(string externalId) => Convert(await Client().Tracks.Get(externalId));
    public async Task<IReadOnlyList<ExternalSong>> Search(string term)
    {
        var result = await Client().Search.Item(new SearchRequest(SearchRequest.Types.Track, term) { Limit = 10 });
        return result.Tracks?.Items?.Where(t => t != null).Select(Convert).ToArray() ?? [];
    }
    private static ExternalSong Convert(FullTrack track) => new() {
        Platform = Platforms.Spotify, ExternalId = track.Id, Title = track.Name,
        Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
        ThumbnailUrl = track.Album.Images.FirstOrDefault()?.Url, Duration = TimeSpan.FromMilliseconds(track.DurationMs)
    };
}
