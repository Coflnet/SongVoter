using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Coflnet.SongVoter.Service;
public class DbHealthCheck : IHealthCheck
{
    private readonly SVContext db;

    public DbHealthCheck(SVContext db)
    {
        this.db = db;
    }
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync();
        var isHealthy = user != null;

        if (isHealthy)
        {
            return HealthCheckResult.Healthy("Db reachable.");
        }

        return 
            new HealthCheckResult(
                context.Registration.FailureStatus, "Db unreachable.");
    }
}
