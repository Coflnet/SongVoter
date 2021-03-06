/*
 * Songvoter
 *
 * Definition for songvoter API
 *
 * The version of the OpenAPI document: 0.0.1
 * Contact: support@coflnet.com
 * Generated by: https://openapi-generator.tech
 */

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;
using Newtonsoft.Json;
using Coflnet.SongVoter.Attributes;
using Coflnet.SongVoter.Models;

namespace Coflnet.SongVoter.Controllers
{ 
    /// <summary>
    /// 
    /// </summary>
    [ApiController]
    public abstract class AuthApiController : ControllerBase
    { 
        /// <summary>
        /// Authenticate with google
        /// </summary>
        /// <remarks>Exchange a google identity token for a songvoter token</remarks>
        /// <param name="authToken">The google identity token</param>
        /// <response code="200">successful operation</response>
        [HttpPost]
        [Route("/v1/auth/google")]
        [Consumes("application/json")]
        [ValidateModelState]
        [SwaggerOperation("AuthWithGoogle")]
        [SwaggerResponse(statusCode: 200, type: typeof(AuthToken), description: "successful operation")]
        public abstract Task<IActionResult> AuthWithGoogle([FromBody]AuthToken authToken);
    }
}
