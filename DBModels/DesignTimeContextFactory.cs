using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Coflnet.SongVoter.DBModels;

public class DesignTimeContextFactory : IDesignTimeDbContextFactory<SVContext>
{
    public SVContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SVContext>()
        .UseNpgsql(System.Environment.GetEnvironmentVariable("DB_CONNECTION") ??
            "Host=localhost;Database=songvoter;Username=songvoter;Password=songvoter").Options);
}
