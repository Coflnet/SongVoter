using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Coflnet.SongVoter.Transformers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Coflnet.SongVoter.Controllers;

[ApiController, Authorize, Route("api/songs")]
public class SongApiController(SVContext db, IDService ids, SongCatalog catalog, SongTransformer transformer) : ControllerBase
{
    public record ImportRequest([Required, StringLength(2048, MinimumLength = 1)] string Url);

    [HttpPost("import")]
    public async Task<IActionResult> Import(ImportRequest request)
    {
        var (platform, id) = SongCatalog.ParseLink(request.Url.Trim());
        return Ok(transformer.ToApiSong(await catalog.Resolve(platform, id)));
    }

    [HttpPost]
    public async Task<IActionResult> Add(Models.SongCreation request)
    {
        var url = request.Platform switch {
            Models.SongPlatform.Spotify => $"https://open.spotify.com/track/{request.ExternalId}",
            Models.SongPlatform.Youtube => $"https://youtu.be/{request.ExternalId}",
            _ => ""
        };
        return await Import(new ImportRequest(url));
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery, Required, StringLength(200, MinimumLength = 2)] string term)
    {
        if (string.IsNullOrWhiteSpace(term) || SongCatalog.Lookup(term).Length < 2) return BadRequest("Enter a song or artist.");
        if (term.StartsWith("https://") || term.StartsWith("spotify:")) return await Import(new ImportRequest(term));
        var result = await catalog.Search(term.Trim());
        return Ok(new { songs = result.Songs.Select(transformer.ToApiSong), warnings = result.Warnings });
    }

    [HttpGet("{songId}")]
    public async Task<IActionResult> Get(string songId)
    {
        var song = await db.Songs.Include(s => s.ExternalSongs).FirstOrDefaultAsync(s => s.Id == ids.FromHash(songId));
        return song == null ? NotFound() : Ok(transformer.ToApiSong(song));
    }
}
