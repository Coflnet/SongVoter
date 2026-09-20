using Coflnet.SongVoter.Middleware;
using Microsoft.AspNetCore.Http;
using Npgsql;
using Xunit;

namespace SongVoter.Tests;

public class MiddlewareTests
{
    [Fact, Trait("Category", "Unit")]
    public async Task WrappedSerializationFailureReturnsRecoverableConflict()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ErrorMiddleware(_ => throw new InvalidOperationException("Transient failure",
            new PostgresException("Concurrent update", "ERROR", "ERROR", "40001")));
        await middleware.Invoke(context);
        Assert.Equal(409, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        Assert.Equal("The queue changed. Please try again.", await new StreamReader(context.Response.Body).ReadToEndAsync());
    }
}
