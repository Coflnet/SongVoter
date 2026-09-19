using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Coflnet.SongVoter.Controllers;

[ApiController, Route("api/config")]
public class ConfigController(IConfiguration configuration) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public object Get() => new { spotifyClientId = configuration["spotify:clientid"], paidPriorityEnabled = false };
}
