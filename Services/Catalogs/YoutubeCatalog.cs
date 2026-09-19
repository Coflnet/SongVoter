using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Configuration;

namespace Coflnet.SongVoter.Service;

public class YoutubeCatalog(IConfiguration configuration) : IMusicCatalog
{
    public Platforms Platform => Platforms.Youtube;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["youtube:apiKey"]);
    private YouTubeService Client() => new(new BaseClientService.Initializer { ApiKey = configuration["youtube:apiKey"], ApplicationName = "SongVoter" });
    public async Task<ExternalSong> Resolve(string externalId) => (await Details([externalId])).FirstOrDefault();
    public async Task<IReadOnlyList<ExternalSong>> Search(string term)
    {
        using var client = Client();
        var search = client.Search.List("snippet");
        search.Q = term;
        search.Type = "video";
        search.VideoEmbeddable = SearchResource.ListRequest.VideoEmbeddableEnum.True__;
        search.MaxResults = 10;
        var result = await search.ExecuteAsync();
        return await Details(result.Items.Select(i => i.Id.VideoId).ToArray());
    }
    private async Task<IReadOnlyList<ExternalSong>> Details(string[] ids)
    {
        if (ids.Length == 0) return [];
        using var client = Client();
        var request = client.Videos.List("snippet,contentDetails,status");
        request.Id = string.Join(",", ids);
        var result = await request.ExecuteAsync();
        return result.Items.Where(v => v.Status.Embeddable == true).Select(v => new ExternalSong {
            Platform = Platforms.Youtube, ExternalId = v.Id, Title = v.Snippet.Title,
            Artist = v.Snippet.ChannelTitle, ThumbnailUrl = (v.Snippet.Thumbnails.High ?? v.Snippet.Thumbnails.Default__)?.Url,
            Duration = System.Xml.XmlConvert.ToTimeSpan(v.ContentDetails.Duration)
        }).ToArray();
    }
}
