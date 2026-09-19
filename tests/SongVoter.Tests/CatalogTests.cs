using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Middleware;
using Coflnet.SongVoter.Service;
using Xunit;

namespace SongVoter.Tests;

public class CatalogTests
{
    [Theory, Trait("Category", "Unit")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=share", Platforms.Youtube, "dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ", Platforms.Youtube, "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", Platforms.Youtube, "dQw4w9WgXcQ")]
    [InlineData("https://open.spotify.com/intl-de/track/4cOdK2wGLETKBW3PvgPWqT?si=share", Platforms.Spotify, "4cOdK2wGLETKBW3PvgPWqT")]
    [InlineData("spotify:track:4cOdK2wGLETKBW3PvgPWqT", Platforms.Spotify, "4cOdK2wGLETKBW3PvgPWqT")]
    public void ParsesSupportedSongLinks(string link, Platforms platform, string id) => Assert.Equal((platform, id), SongCatalog.ParseLink(link));

    [Theory, Trait("Category", "Unit")]
    [InlineData("https://youtube.com.evil.test/watch?v=dQw4w9WgXcQ")]
    [InlineData("http://127.0.0.1/private")]
    [InlineData("https://open.spotify.com/playlist/4cOdK2wGLETKBW3PvgPWqT")]
    public void RejectsOtherHostsAndNonTracks(string link) => Assert.Throws<ApiException>(() => SongCatalog.ParseLink(link));
}
