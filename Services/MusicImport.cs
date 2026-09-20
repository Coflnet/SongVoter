using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Middleware;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Coflnet.SongVoter.Service;

// Only fixed provider endpoints are called. OAuth credentials never reach the browser.
public class MusicImport(SVContext db, IConfiguration config, IHttpClientFactory clients)
{
    public record Pending(string ProofHash, string Verifier, bool Native, string Language, DateTime TokenExpiry);
    public record MusicList(string Id, string Name);
    public record ListPage(IReadOnlyList<MusicList> Lists, string Next);
    public record SongPage(IReadOnlyList<ExternalSong> Songs, string Next);
    public static Platforms Platform(string provider) => provider switch {
        "youtube" => Platforms.Youtube, "spotify" => Platforms.Spotify,
        _ => throw new ApiException(HttpStatusCode.NotFound, "Unknown music service.")
    };
    public static string Marker(string provider) => "songvoter-import:" + provider;
    public static string PendingMarker(string state) => "songvoter-import-pending:" + Hash(state);
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Random() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public string ClientId(string provider) => config[provider == "youtube" ? "google:clientid" : "spotify:clientid"];
    private string ClientSecret(string provider) => config[provider == "youtube" ? "google:clientsecret" : "spotify:clientsecret"];
    public bool Configured(string provider) => !string.IsNullOrWhiteSpace(ClientId(provider)) && !string.IsNullOrWhiteSpace(ClientSecret(provider));
    public static string Callback(string provider) => $"https://songvoter.party/api/import/{provider}/callback";

    // Use the deployment's stable secret so encrypted credentials survive pod replacement.
    public string Protect(string value)
    {
        var plain = Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(SHA256.HashData(Encoding.UTF8.GetBytes("songvoter-import-v1:" + config["jwt:secret"])), 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return "v1:" + Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }
    public string Unprotect(string value)
    {
        var bytes = Convert.FromBase64String(value[3..]);
        var plain = new byte[bytes.Length - 28];
        using var aes = new AesGcm(SHA256.HashData(Encoding.UTF8.GetBytes("songvoter-import-v1:" + config["jwt:secret"])), 16);
        aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plain);
        return Encoding.UTF8.GetString(plain);
    }

    public async Task<(string Url, string State)> Start(User user, string provider, string proof, bool native, string language)
    {
        var platform = Platform(provider);
        if (!Configured(provider)) throw new ApiException(HttpStatusCode.ServiceUnavailable, "Account import is not connected yet. You can still import a YouTube playlist link.");
        await db.Set<Oauth2Token>().Where(t => t.ExternalId.StartsWith("songvoter-import-pending:") && t.Expiration < DateTime.UtcNow).ExecuteDeleteAsync();
        var state = Random();
        var verifier = Random();
        db.Add(new Oauth2Token {
            User = user, Platform = platform, ExternalId = PendingMarker(state), Scropes = "pending",
            AuthCode = Protect(JsonSerializer.Serialize(new Pending(Hash(proof), verifier, native, language, default))),
            Expiration = DateTime.UtcNow.AddMinutes(10)
        });
        await db.SaveChangesAsync();
        var query = new Dictionary<string, string> {
            ["client_id"] = ClientId(provider), ["redirect_uri"] = Callback(provider), ["response_type"] = "code", ["state"] = state,
            ["code_challenge_method"] = "S256", ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
            ["scope"] = provider == "youtube" ? "https://www.googleapis.com/auth/youtube.readonly" : "playlist-read-private playlist-read-collaborative user-library-read"
        };
        if (provider == "youtube") { query["access_type"] = "offline"; query["prompt"] = "consent"; }
        return (QueryHelpers.AddQueryString(provider == "youtube" ? "https://accounts.google.com/o/oauth2/v2/auth" : "https://accounts.spotify.com/authorize", query), state);
    }

    public async Task<string> CallbackResult(string provider, string state, string code, string error)
    {
        var platform = Platform(provider);
        var marker = PendingMarker(state ?? "");
        var pending = await db.Set<Oauth2Token>().SingleOrDefaultAsync(t => t.ExternalId == marker && t.Platform == platform && t.Expiration > DateTime.UtcNow);
        if (pending == null) return "/app?oauthError=expired";
        var data = JsonSerializer.Deserialize<Pending>(Unprotect(pending.AuthCode));
        // Claim the callback once, across replicas. Completing still requires the originating device's proof and JWT.
        if (await db.Set<Oauth2Token>().Where(t => t.Id == pending.Id && t.Scropes == "pending").ExecuteUpdateAsync(s => s.SetProperty(t => t.Scropes, "exchanging")) == 1) {
            pending.Scropes = "cancelled";
            if (string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(code)) {
                try {
                    var token = await Token(provider, new() { ["grant_type"] = "authorization_code", ["code"] = code, ["code_verifier"] = data.Verifier, ["redirect_uri"] = Callback(provider) });
                    pending.AccessToken = Protect(token.GetProperty("access_token").GetString());
                    pending.RefreshToken = token.TryGetProperty("refresh_token", out var refresh) ? Protect(refresh.GetString()) : null;
                    pending.AuthCode = Protect(JsonSerializer.Serialize(data with { TokenExpiry = DateTime.UtcNow.AddSeconds(token.GetProperty("expires_in").GetInt32()) }));
                    pending.Scropes = "complete";
                } catch (ApiException) { pending.Scropes = "failed"; }
            }
            await db.SaveChangesAsync();
        }
        var destination = data.Native ? "com.coflnet.songvoter://import-callback" : "/app";
        return QueryHelpers.AddQueryString(destination, new Dictionary<string, string> { ["import"] = provider, ["state"] = state, ["lang"] = data.Language });
    }

    public async Task Complete(int userId, string provider, string state, string proof)
    {
        var platform = Platform(provider);
        using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var marker = PendingMarker(state);
        var token = await db.Set<Oauth2Token>().SingleOrDefaultAsync(t => t.User.Id == userId && t.ExternalId == marker && t.Platform == platform && t.Expiration > DateTime.UtcNow);
        if (token == null || JsonSerializer.Deserialize<Pending>(Unprotect(token.AuthCode)).ProofHash != Hash(proof))
            throw new ApiException(HttpStatusCode.BadRequest, "Return to the browser or device where you started connecting.");
        if (token.Scropes != "complete") throw new ApiException(HttpStatusCode.BadRequest,
            token.Scropes == "cancelled" ? "Connection cancelled. Choose a service to try again." : "The music account could not connect. Please try again.");
        await db.Set<Oauth2Token>().Where(t => t.User.Id == userId && t.ExternalId == Marker(provider)).ExecuteDeleteAsync();
        token.ExternalId = Marker(provider);
        token.Expiration = JsonSerializer.Deserialize<Pending>(Unprotect(token.AuthCode)).TokenExpiry;
        token.AuthCode = null;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<JsonElement> Token(string provider, Dictionary<string, string> fields)
    {
        fields["client_id"] = ClientId(provider);
        fields["client_secret"] = ClientSecret(provider);
        using var request = new HttpRequestMessage(HttpMethod.Post, provider == "youtube" ? "https://oauth2.googleapis.com/token" : "https://accounts.spotify.com/api/token") { Content = new FormUrlEncodedContent(fields) };
        return await Send(request);
    }

    private async Task<string> Access(int userId, string provider)
    {
        var marker = Marker(provider);
        var token = await db.Set<Oauth2Token>().SingleOrDefaultAsync(t => t.User.Id == userId && t.ExternalId == marker);
        if (token == null) throw new ApiException(HttpStatusCode.Conflict, "Connect your music account to choose a list.");
        if (token.Expiration < DateTime.UtcNow.AddMinutes(1)) {
            if (token.RefreshToken == null) throw new ApiException(HttpStatusCode.Conflict, "Reconnect your music account to continue.");
            JsonElement refreshed;
            try { refreshed = await Token(provider, new() { ["grant_type"] = "refresh_token", ["refresh_token"] = Unprotect(token.RefreshToken) }); }
            catch (ApiException) { throw new ApiException(HttpStatusCode.Conflict, "Reconnect your music account to continue."); }
            token.AccessToken = Protect(refreshed.GetProperty("access_token").GetString());
            if (refreshed.TryGetProperty("refresh_token", out var refresh)) token.RefreshToken = Protect(refresh.GetString());
            token.Expiration = DateTime.UtcNow.AddSeconds(refreshed.GetProperty("expires_in").GetInt32());
            await db.SaveChangesAsync();
        }
        return Unprotect(token.AccessToken);
    }

    private async Task<JsonElement> Send(HttpRequestMessage request)
    {
        try {
            using var response = await clients.CreateClient("music-import").SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new ApiException(response.StatusCode == HttpStatusCode.Unauthorized ? HttpStatusCode.Conflict : HttpStatusCode.BadGateway,
                response.StatusCode == HttpStatusCode.Unauthorized ? "Reconnect your music account to continue." : "The music service could not open this list. Check account access or try another playlist.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.Clone();
        } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) {
            throw new ApiException(HttpStatusCode.BadGateway, "The music service is taking too long. Please try again.");
        }
    }

    private async Task<JsonElement> Get(string provider, string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, (provider == "youtube" ? "https://www.googleapis.com/youtube/v3/" : "https://api.spotify.com/v1/") + path);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await Send(request);
    }
    private static string Cursor(string cursor) => Uri.EscapeDataString(cursor ?? "");
    private static int Offset(string cursor) => int.TryParse(cursor, out var n) && n >= 0 && n <= 10000 ? n : 0;
    private static string Next(JsonElement json, string provider, int offset) => provider == "youtube"
        ? json.TryGetProperty("nextPageToken", out var page) ? page.GetString() : null
        : json.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String ? (offset + 50).ToString() : null;

    public async Task<ListPage> Lists(int userId, string provider, string cursor)
    {
        Platform(provider);
        var access = await Access(userId, provider);
        var json = await Get(provider, provider == "youtube" ? $"playlists?part=snippet&mine=true&maxResults=50&pageToken={Cursor(cursor)}" : $"me/playlists?limit=50&offset={Offset(cursor)}", access);
        var lists = new List<MusicList>();
        if (string.IsNullOrEmpty(cursor)) lists.Add(new("liked", "Liked songs"));
        string owner = provider == "spotify" ? (await Get(provider, "me", access)).GetProperty("id").GetString() : null;
        foreach (var item in json.GetProperty("items").EnumerateArray()) {
            if (item.ValueKind == JsonValueKind.Null) continue;
            if (provider == "spotify" && item.GetProperty("owner").GetProperty("id").GetString() != owner && !item.GetProperty("collaborative").GetBoolean()) continue;
            lists.Add(new(item.GetProperty("id").GetString(), (provider == "youtube" ? item.GetProperty("snippet").GetProperty("title") : item.GetProperty("name")).GetString()));
        }
        return new(lists, Next(json, provider, Offset(cursor)));
    }

    public async Task<SongPage> ReadSongs(int userId, string provider, string list, string cursor, bool publicLink)
    {
        Platform(provider);
        if (!Regex.IsMatch(list, provider == "spotify" ? "^(liked|[A-Za-z0-9]{22})$" : "^(liked|[A-Za-z0-9_-]{10,100})$"))
            throw new ApiException(HttpStatusCode.BadRequest, "Choose a playlist or paste its full link.");
        var token = publicLink && provider == "youtube" ? null : await Access(userId, provider);
        if (provider == "spotify") {
            var json = await Get(provider, (list == "liked" ? "me/tracks" : $"playlists/{list}/items") + $"?limit=50&offset={Offset(cursor)}", token);
            var tracks = new List<ExternalSong>();
            foreach (var item in json.GetProperty("items").EnumerateArray()) {
                // Spotify renamed track to item for the 2026 playlist endpoint; saved tracks still use track.
                if (!item.TryGetProperty("item", out var track) && !item.TryGetProperty("track", out track)) continue;
                if (track.ValueKind != JsonValueKind.Object || !track.TryGetProperty("type", out var type) || type.GetString() != "track" || track.GetProperty("id").ValueKind != JsonValueKind.String) continue;
                if (track.TryGetProperty("is_local", out var local) && local.GetBoolean()) continue;
                if (track.TryGetProperty("is_playable", out var playable) && !playable.GetBoolean()) continue;
                var image = track.GetProperty("album").GetProperty("images").EnumerateArray().FirstOrDefault();
                tracks.Add(new ExternalSong { Platform = Platforms.Spotify, ExternalId = track.GetProperty("id").GetString(), Title = track.GetProperty("name").GetString(),
                    Artist = string.Join(", ", track.GetProperty("artists").EnumerateArray().Select(a => a.GetProperty("name").GetString())),
                    ThumbnailUrl = image.ValueKind == JsonValueKind.Object ? image.GetProperty("url").GetString() : null,
                    Duration = TimeSpan.FromMilliseconds(track.GetProperty("duration_ms").GetInt32()) });
            }
            return new(tracks, Next(json, provider, Offset(cursor)));
        }
        var key = token == null ? "&key=" + Uri.EscapeDataString(config["youtube:apiKey"] ?? "") : "";
        if (list == "liked" && token == null) throw new ApiException(HttpStatusCode.BadRequest, "Connect your music account to choose a list.");
        var path = list == "liked" ? "videos?part=snippet,contentDetails,status&myRating=like" : $"playlistItems?part=contentDetails&playlistId={list}";
        var result = await Get(provider, path + $"&maxResults=50&pageToken={Cursor(cursor)}" + key, token);
        var videos = result;
        string[] orderedIds = null;
        if (list != "liked") {
            orderedIds = result.GetProperty("items").EnumerateArray().Select(v => v.GetProperty("contentDetails").GetProperty("videoId").GetString()).Distinct().ToArray();
            if (orderedIds.Length == 0) return new([], Next(result, provider, 0));
            videos = await Get(provider, "videos?part=snippet,contentDetails,status&id=" + string.Join(",", orderedIds) + key, token);
        }
        var songs = videos.GetProperty("items").EnumerateArray().Where(v => v.GetProperty("status").GetProperty("embeddable").GetBoolean()).Select(v => {
            var snippet = v.GetProperty("snippet");
            var thumbs = snippet.GetProperty("thumbnails");
            return new ExternalSong { Platform = Platforms.Youtube, ExternalId = v.GetProperty("id").GetString(), Title = snippet.GetProperty("title").GetString(),
                Artist = snippet.GetProperty("channelTitle").GetString(), ThumbnailUrl = (thumbs.TryGetProperty("high", out var thumb) ? thumb : thumbs.GetProperty("default")).GetProperty("url").GetString(),
                Duration = System.Xml.XmlConvert.ToTimeSpan(v.GetProperty("contentDetails").GetProperty("duration").GetString()) };
        }).ToArray();
        return new(orderedIds == null ? songs : songs.OrderBy(s => Array.IndexOf(orderedIds, s.ExternalId)).ToArray(), Next(result, provider, 0));
    }

    public static (string Provider, string Id) ParsePlaylist(string input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new ApiException(HttpStatusCode.BadRequest, "Choose a playlist or paste its full link.");
        if (uri.Host is "youtube.com" or "www.youtube.com" or "music.youtube.com" or "m.youtube.com" or "youtu.be") {
            if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("list", out var id) && Regex.IsMatch(id.ToString(), "^[A-Za-z0-9_-]{10,100}$")) return ("youtube", id.ToString());
        }
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (uri.Host == "open.spotify.com" && parts.Length >= 2 && parts[^2] == "playlist" && Regex.IsMatch(parts[^1], "^[A-Za-z0-9]{22}$")) return ("spotify", parts[^1]);
        throw new ApiException(HttpStatusCode.BadRequest, "Choose a playlist or paste its full link.");
    }
}
