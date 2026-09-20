using Coflnet.SongVoter.Middleware;
using Microsoft.AspNetCore.Http;
using Npgsql;
using Xunit;

namespace SongVoter.Tests;

public class MiddlewareTests
{
    [Theory, Trait("Category", "Unit")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WrappedSerializationFailureReturnsRecoverableConflict(bool saving)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        Exception cause = new PostgresException("Concurrent update", "ERROR", "ERROR", "40001");
        if (saving) cause = new Microsoft.EntityFrameworkCore.DbUpdateException("Update failed", cause);
        var middleware = new ErrorMiddleware(_ => throw new InvalidOperationException("Transient failure", cause));
        await middleware.Invoke(context);
        Assert.Equal(409, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        Assert.Equal("The queue changed. Please try again.", await new StreamReader(context.Response.Body).ReadToEndAsync());
    }
}
