using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Coflnet.SongVoter;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace SongVoter.Tests;

public class EndToEndTests
{
    private sealed class Catalog(Platforms platform) : IMusicCatalog
    {
        public Platforms Platform => platform;
        public bool IsConfigured => true;
        public Task<IReadOnlyList<ExternalSong>> Search(string term) => Task.FromResult<IReadOnlyList<ExternalSong>>([]);
        public Task<ExternalSong> Resolve(string id) => Task.FromResult(new ExternalSong {
            Platform = platform, ExternalId = id, Title = $"{platform} {id}", Artist = "Test artist", Duration = TimeSpan.FromMinutes(3)
        });
    }

    internal sealed class App(string connection, HttpMessageHandler importHandler = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string> {
                ["DB_CONNECTION"] = connection, ["jwt:secret"] = "songvoter-integration-tests-only-32-byte-secret",
                ["hashids:salt"] = "songvoter-tests", ["Authentication:Difficulty"] = "16",
                ["google:clientid"] = "test-google", ["google:clientsecret"] = "test-only",
                ["spotify:clientid"] = "test-spotify", ["spotify:clientsecret"] = "test-only"
            }));
            builder.ConfigureServices(services => {
                services.RemoveAll<IMusicCatalog>();
                services.AddSingleton<IMusicCatalog>(new Catalog(Platforms.Youtube));
                services.AddSingleton<IMusicCatalog>(new Catalog(Platforms.Spotify));
                if (importHandler != null) services.AddHttpClient("music-import").ConfigurePrimaryHttpMessageHandler(() => importHandler);
            });
        }
    }

    internal static async Task<JsonObject> Json(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return JsonNode.Parse(body).AsObject();
    }
    internal static string Str(JsonObject value, string key) => value[key].GetValue<string>();

    internal static async Task<(JsonObject Session, object Proof)> Login(HttpClient client, string secret = null)
    {
        secret ??= Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var challenge = await Json(await client.PostAsJsonAsync("/api/auth/challenge", new { identityHash = GuestAuthentication.IdentityHash(secret) }));
        var data = new AuthChallenge { Id = Str(challenge, "id"), IdentityHash = GuestAuthentication.IdentityHash(secret),
            Difficulty = challenge["difficulty"].GetValue<int>(), ExpiresAt = DateTime.UtcNow.AddMinutes(5) };
        long counter = 0;
        while (!GuestAuthentication.Verify(data, secret, counter, DateTime.UtcNow)) counter++;
        var proof = new { challengeId = data.Id, secret, counter };
        var session = await Json(await client.PostAsJsonAsync("/api/auth/anonymous", proof));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Str(session, "token"));
        return (session, proof);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task GuestsJoinImportVoteAndHostAutomaticallyAdvancesWithPersistentIsolation()
    {
        var connection = Environment.GetEnvironmentVariable("SV_TEST_DATABASE")
            ?? throw new InvalidOperationException("Set SV_TEST_DATABASE to an isolated PostgreSQL test server, or run --filter Category=Unit.");
        var name = "songvoter_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(connection) { Database = name };
        try {
            await using var app = new App(builder.ConnectionString);
            using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<SVContext>().Database.MigrateAsync();
            using var host = app.CreateClient();
            using var guest = app.CreateClient();
            using var stranger = app.CreateClient();
            Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/status")).StatusCode); // Empty database is healthy.
            Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/lists")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/auth/anonymous?nonce=guessable", new { })).StatusCode);
            var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            var login = await Login(host, secret);
            Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsJsonAsync("/api/auth/anonymous", login.Proof)).StatusCode);
            var resumed = await Login(host, secret);
            Assert.Equal(Str(login.Session, "userId"), Str(resumed.Session, "userId"));
            await Login(guest);
            await Login(stranger);
            var favouriteList = await Json(await host.GetAsync("/api/lists/favourites"));
            var listId = Str(favouriteList, "id");
            Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/lists/{listId}")).StatusCode);
            var imports = await Task.WhenAll(
                host.PostAsJsonAsync("/api/songs/import", new { url = "https://youtu.be/dQw4w9WgXcQ" }),
                guest.PostAsJsonAsync("/api/songs/import", new { url = "https://youtu.be/dQw4w9WgXcQ" }));
            Assert.All(imports, response => Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict));
            var hostSong = await Json(imports.First(response => response.IsSuccessStatusCode));
            var duplicate = await Json(await guest.PostAsJsonAsync("/api/songs/import", new { url = "https://youtube.com/watch?v=dQw4w9WgXcQ" }));
            Assert.Equal(Str(hostSong, "id"), Str(duplicate, "id"));
            var id = Str(hostSong, "id");
            Assert.Equal(HttpStatusCode.NotFound, (await guest.PostAsJsonAsync($"/api/lists/{listId}/songs", new { id })).StatusCode);
            await Json(await host.PostAsJsonAsync($"/api/lists/{listId}/songs", new { id }));
            var creations = await Task.WhenAll(
                host.PostAsJsonAsync("/api/party", new { name = "Kitchen party", supportedPlatforms = new[] { "youtube" } }),
                host.PostAsJsonAsync("/api/party", new { name = "Kitchen party", supportedPlatforms = new[] { "youtube" } }));
            Assert.Single(creations, response => response.IsSuccessStatusCode);
            Assert.Single(creations, response => response.StatusCode == HttpStatusCode.Conflict);
            var party = await Json(creations.Single(response => response.IsSuccessStatusCode));
            Assert.StartsWith("https://songvoter.party/join/", Str(party, "joinUrl"));
            Assert.Single(party["queue"].AsArray());
            Assert.Equal(3, party["queue"][0]["score"].GetValue<double>());
            var code = Str(party, "code");
            await Json(await guest.PostAsync($"/api/party/{code}/join", null));
            await Json(await guest.PostAsync($"/api/party/{code}/join", null)); // Idempotent join.
            Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync("/api/party")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await guest.PostAsJsonAsync("/api/party/next", new { version = 0 })).StatusCode);
            var guestSong = await Json(await guest.PostAsJsonAsync("/api/songs/import", new { url = "https://youtu.be/aqz-KE-bpKQ" }));
            var guestId = Str(guestSong, "id");
            party = await Json(await guest.PostAsJsonAsync("/api/party/add", new[] { guestId }));
            Assert.Equal(1, party["queue"][1]["score"].GetValue<double>());
            party = await Json(await guest.PostAsJsonAsync("/api/party/add", new[] { guestId, guestId }));
            Assert.Equal(2, party["queue"].AsArray().Count);
            var spotify = await Json(await guest.PostAsJsonAsync("/api/songs/import", new { url = "spotify:track:4cOdK2wGLETKBW3PvgPWqT" }));
            party = await Json(await guest.PostAsJsonAsync("/api/party/add", new[] { Str(spotify, "id") }));
            Assert.False(party["queue"].AsArray().Single(q => q["song"]["id"].GetValue<string>() == Str(spotify, "id"))["playable"].GetValue<bool>());
            Assert.Equal(0.5, party["queue"].AsArray().Single(q => q["song"]["id"].GetValue<string>() == guestId)["score"].GetValue<double>());
            party = await Json(await host.PostAsJsonAsync("/api/party/next", new { version = 0 }));
            Assert.Equal(id, party["currentSong"]["id"].GetValue<string>());
            Assert.Equal(HttpStatusCode.Conflict, (await host.PostAsJsonAsync("/api/party/next", new { version = 0 })).StatusCode);
            party = await Json(await host.PostAsJsonAsync("/api/party/next", new { version = 1 }));
            Assert.Equal(guestId, party["currentSong"]["id"].GetValue<string>());
            // Concurrent end callbacks cannot advance twice.
            var advances = await Task.WhenAll(host.PostAsJsonAsync("/api/party/next", new { version = 2 }), host.PostAsJsonAsync("/api/party/next", new { version = 2 }));
            Assert.Single(advances, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(advances, r => r.StatusCode == HttpStatusCode.Conflict);
            await Json(await guest.PostAsync($"/api/party/removeVote/{guestId}", null));
            Assert.Equal(HttpStatusCode.NoContent, (await guest.PostAsync("/api/party/leave", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await host.PostAsync("/api/party/leave", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await guest.PostAsync($"/api/party/{code}/join", null)).StatusCode);
            var saved = await Json(await host.GetAsync($"/api/lists/{listId}"));
            Assert.Single(saved["songs"].AsArray());
            Assert.Equal(HttpStatusCode.BadRequest, (await host.PostAsJsonAsync("/api/songs/import", new { url = "https://127.0.0.1/private" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/api/openapi/v1.json")).StatusCode);
        } finally {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
