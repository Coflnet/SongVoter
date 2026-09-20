using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Middleware;
using Coflnet.SongVoter.Service;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using static SongVoter.Tests.EndToEndTests;

namespace SongVoter.Tests;

public class MusicImportTests
{
    private sealed class Provider : HttpMessageHandler
    {
        public int Exchanges;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            object body;
            if (uri.AbsolutePath.EndsWith("token")) {
                var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
                Assert.Equal("authorization_code", form["grant_type"]);
                Assert.Equal(43, form["code_verifier"].ToString().Length);
                Exchanges++;
                body = new { access_token = "private-provider-token", refresh_token = "private-refresh-token", expires_in = 3600 };
            } else if (uri.AbsolutePath.EndsWith("/me")) body = new { id = "owner" };
            else if (uri.AbsolutePath.EndsWith("/me/playlists")) body = new { items = new[] { new { id = "0123456789012345678901", name = "Kitchen favourites", owner = new { id = "owner" }, collaborative = false } }, next = (string)null };
            else if (uri.AbsolutePath.EndsWith("/me/tracks") || uri.AbsolutePath.EndsWith("/items")) {
                Assert.Equal("private-provider-token", request.Headers.Authorization?.Parameter);
                body = new { items = Enumerable.Range(0, 40).Select(n => new { item = new { type = "track", id = n.ToString("D22"), name = $"Song {n}", artists = new[] { new { name = "Artist" } }, album = new { images = Array.Empty<object>() }, duration_ms = 180000, is_local = false, is_playable = true } }).ToArray(), next = (string)null };
            } else if (uri.AbsolutePath.EndsWith("/playlists")) body = new { items = new[] { new { id = "PL0123456789", snippet = new { title = "YouTube favourites" } } } };
            else if (uri.AbsolutePath.EndsWith("/playlistItems")) body = new { items = new[] { new { contentDetails = new { videoId = "dX3k_QDnzHE" } }, new { contentDetails = new { videoId = "dX3k_QDnzHE" } } } };
            else if (uri.AbsolutePath.EndsWith("/videos")) body = new { items = new[] { new { id = "dX3k_QDnzHE", status = new { embeddable = true }, contentDetails = new { duration = "PT4M" }, snippet = new { title = "Midnight City", channelTitle = "M83", thumbnails = new { high = new { url = "https://i.ytimg.com/vi/dX3k_QDnzHE/hqdefault.jpg" } } } } } };
            else throw new InvalidOperationException("Unexpected provider endpoint: " + uri.GetLeftPart(UriPartial.Path));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
        }
    }

    [Theory, Trait("Category", "Unit")]
    [InlineData("https://www.youtube.com/playlist?list=PL0123456789", "youtube", "PL0123456789")]
    [InlineData("https://music.youtube.com/watch?v=dX3k_QDnzHE&list=PL0123456789", "youtube", "PL0123456789")]
    [InlineData("https://open.spotify.com/intl-de/playlist/0123456789012345678901?si=abc", "spotify", "0123456789012345678901")]
    public void ParsesPlaylistLinks(string link, string provider, string id) => Assert.Equal((provider, id), MusicImport.ParsePlaylist(link));

    [Theory, Trait("Category", "Unit")]
    [InlineData("https://youtube.com.evil.test/playlist?list=PL0123456789")]
    [InlineData("http://127.0.0.1/playlist?list=PL0123456789")]
    [InlineData("https://open.spotify.com/track/0123456789012345678901")]
    public void RejectsUntrustedPlaylistLinks(string link) => Assert.Throws<ApiException>(() => MusicImport.ParsePlaylist(link));

    [Fact, Trait("Category", "Integration")]
    public async Task OAuthIsBoundToTheOriginatingProfileAndImportsRespectFavouriteLimits()
    {
        var connection = Environment.GetEnvironmentVariable("SV_TEST_DATABASE") ?? throw new InvalidOperationException("Set SV_TEST_DATABASE.");
        var name = "songvoter_import_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await command.ExecuteNonQueryAsync();
        try {
            var provider = new Provider();
            await using var app = new App(new NpgsqlConnectionStringBuilder(connection) { Database = name }.ConnectionString, provider);
            using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<SVContext>().Database.MigrateAsync();
            using var guest = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var stranger = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            await Login(guest);
            await Login(stranger);
            var party = await Json(await stranger.PostAsJsonAsync("/api/party", new { name = "Import party", supportedPlatforms = new[] { "youtube", "spotify" } }));
            await Json(await guest.PostAsync($"/api/party/{Str(party, "code")}/join", null));
            var proof = new string('a', 64);
            foreach (var service in new[] { "youtube", "spotify" }) {
                var start = await Json(await guest.PostAsJsonAsync($"/api/import/{service}/connect", new { proof, native = service == "youtube", language = "de" }));
                var state = Str(start, "state");
                var query = QueryHelpers.ParseQuery(new Uri(Str(start, "url")).Query);
                Assert.Equal("S256", query["code_challenge_method"]);
                Assert.Equal(MusicImport.Callback(service), query["redirect_uri"]);
                var callback = await stranger.GetAsync($"/api/import/{service}/callback?state={state}&code=provider-code");
                Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
                Assert.StartsWith(service == "youtube" ? "com.coflnet.songvoter://import-callback" : "/app", callback.Headers.Location!.ToString());
                Assert.Contains("lang=de", callback.Headers.Location.ToString());
                var receipt = QueryHelpers.ParseQuery(new Uri(new Uri("https://songvoter.party"), callback.Headers.Location).Query)["receipt"].ToString();
                Assert.Equal(43, receipt.Length);
                Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync($"/api/import/{service}/complete", new { state, proof })).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync($"/api/import/{service}/complete", new { state, proof, receipt = new string('x', 43) })).StatusCode);
                var exchanges = provider.Exchanges;
                var replay = await stranger.GetAsync($"/api/import/{service}/callback?state={state}&code=provider-code");
                Assert.DoesNotContain("receipt=", replay.Headers.Location!.ToString());
                Assert.Equal(exchanges, provider.Exchanges); // Callback replay cannot exchange again.
                Assert.Equal(HttpStatusCode.BadRequest, (await stranger.PostAsJsonAsync($"/api/import/{service}/complete", new { state, proof, receipt })).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync($"/api/import/{service}/complete", new { state, proof = new string('b', 64), receipt })).StatusCode);
                Assert.Equal(HttpStatusCode.NoContent, (await guest.PostAsJsonAsync($"/api/import/{service}/complete", new { state, proof, receipt })).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync($"/api/import/{service}/complete", new { state, proof, receipt })).StatusCode);
                var lists = await Json(await guest.GetAsync($"/api/import/{service}/lists"));
                Assert.True(lists["connected"]!.GetValue<bool>());
                Assert.Equal("liked", lists["lists"]![0]!["id"]!.GetValue<string>());
                Assert.False((await Json(await stranger.GetAsync($"/api/import/{service}/lists")))["connected"]!.GetValue<bool>());
            }
            using (var scope = app.Services.CreateScope()) {
                var tokens = await scope.ServiceProvider.GetRequiredService<SVContext>().Set<Oauth2Token>().ToListAsync();
                Assert.All(tokens, token => { Assert.StartsWith("v1:", token.AccessToken); Assert.DoesNotContain("private", token.AccessToken); Assert.Null(token.AuthCode); });
            }
            var imported = await Json(await guest.PostAsJsonAsync("/api/import/youtube", new { url = "https://www.youtube.com/playlist?list=PL0123456789" }));
            Assert.Equal(1, imported["added"]!.GetValue<int>());
            imported = await Json(await guest.PostAsJsonAsync("/api/import/youtube", new { listId = "liked" }));
            Assert.Equal(0, imported["added"]!.GetValue<int>());
            imported = await Json(await guest.PostAsJsonAsync("/api/import/spotify", new { listId = "liked" }));
            Assert.Equal(29, imported["added"]!.GetValue<int>());
            Assert.Equal(30, imported["total"]!.GetValue<int>());
            Assert.True(imported["limitReached"]!.GetValue<bool>());
            Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/import/spotify", new { listId = "liked" })).StatusCode);
            var queue = await Json(await guest.GetAsync("/api/party"));
            Assert.Equal(30, queue["queue"]!.AsArray().Count);
            // The API rounds each displayed score to three decimals; thirty favourites still share one vote.
            Assert.All(queue["queue"]!.AsArray(), q => Assert.Equal(0.033, q!["score"]!.GetValue<double>()));
            Assert.Empty((await Json(await stranger.GetAsync("/api/lists/favourites")))["songs"]!.AsArray());
            Assert.Equal(HttpStatusCode.NoContent, (await guest.DeleteAsync("/api/import/spotify")).StatusCode);
            Assert.False((await Json(await guest.GetAsync("/api/import/spotify/lists")))["connected"]!.GetValue<bool>());
            Assert.Equal(30, (await Json(await guest.GetAsync("/api/lists/favourites")))["songs"]!.AsArray().Count);
            var cancelled = await Json(await guest.PostAsJsonAsync("/api/import/spotify/connect", new { proof }));
            var cancelState = Str(cancelled, "state");
            var cancelledReturn = await guest.GetAsync($"/api/import/spotify/callback?state={cancelState}&error=access_denied");
            var cancelledReceipt = QueryHelpers.ParseQuery(new Uri(new Uri("https://songvoter.party"), cancelledReturn.Headers.Location!).Query)["receipt"].ToString();
            var cancelledResult = await guest.PostAsJsonAsync("/api/import/spotify/complete", new { state = cancelState, proof, receipt = cancelledReceipt });
            Assert.Equal(HttpStatusCode.BadRequest, cancelledResult.StatusCode);
            Assert.Contains("Connection cancelled", await cancelledResult.Content.ReadAsStringAsync());
            Assert.DoesNotContain("evil.test", (await guest.GetAsync("/api/import/spotify/callback?state=invalid&error=https://evil.test")).Headers.Location!.ToString());
            var otherStart = await Json(await stranger.PostAsJsonAsync("/api/import/spotify/connect", new { proof }));
            Assert.Equal(HttpStatusCode.NoContent, (await guest.DeleteAsync("/api/user")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/user/info")).StatusCode);
            using (var scope = app.Services.CreateScope()) {
                var remaining = await scope.ServiceProvider.GetRequiredService<SVContext>().Set<Oauth2Token>().ToListAsync();
                Assert.Equal(MusicImport.PendingMarker(Str(otherStart, "state")), Assert.Single(remaining).ExternalId);
            }
            Assert.Equal(HttpStatusCode.NoContent, (await stranger.DeleteAsync("/api/user")).StatusCode);
        } finally {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
