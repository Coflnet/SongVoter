using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coflnet.SongVoter.Middleware
{
    public class ErrorMiddleware
    {
        private readonly RequestDelegate _next;

        public ErrorMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ApiException ex)
            {
                context.Response.ContentType = "text/plain";
                context.Response.StatusCode = (int)ex.StatusCode;
                context.RequestServices.GetRequiredService<ILogger<ErrorMiddleware>>()
                    .LogError(ex, "ApiException");
                await context.Response.WriteAsync(ex.Message);
            }
        }
    }

    public class ApiException : System.Exception
    {
        public HttpStatusCode StatusCode;

        public ApiException(HttpStatusCode statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}

