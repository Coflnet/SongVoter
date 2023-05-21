using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Coflnet.SongVoter.Attributes;
using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Service;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Coflnet.SongVoter.Controllers;
public class UserController : ControllerBase
{
    private readonly SVContext db;
    private IDService idService;
    public UserController(SVContext data, IDService idService)
    {
        this.db = data;
        this.idService = idService;
    }
    /// <summary>
    /// Updates the display name of the current user
    /// </summary>
    /// <response code="200">successful created</response>
    [HttpPost]
    [Route("/user/name")]
    [ValidateModelState]
    [SwaggerOperation("UpdateUserName")]
    [SwaggerResponse(statusCode: 200, type: typeof(User), description: "successful updated")]
    public async Task<IActionResult> UpdateName([FromBody, Required] string name)
    {
        var user = await db.Users.FindAsync((int)idService.UserId(this));
        user.Name = name;
        db.Update(user);
        await db.SaveChangesAsync();
        return Ok();
    }
}