using Coflnet.SongVoter.DBModels;
using Coflnet.SongVoter.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace SongVoter.Tests;

public class LegacyMigrationTests
{
    [Theory, Trait("Category", "Integration")]
    [InlineData("SV_TEST_DATABASE")]
    [InlineData("SV_TEST_COCKROACH")]
    public async Task UpgradePreservesVotesAndPlayCountsAndCanRunAgain(string environment)
    {
        var connection = Environment.GetEnvironmentVariable(environment);
        Assert.NotNull(connection);
        var name = "songvoter_legacy_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        try {
            var target = new NpgsqlConnectionStringBuilder(connection) { Database = name };
            await using var db = new SVContext(new DbContextOptionsBuilder<SVContext>().UseNpgsql(target.ConnectionString).Options);
            // Recreate the real pre-refresh schema, before its queue uniqueness constraint.
            var script = db.GetService<IMigrator>().GenerateScript("0", "20230829170906_AddLookup", MigrationsSqlGenerationOptions.NoTransactions);
            foreach (var sql in script.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                await db.Database.ExecuteSqlRawAsync(sql);
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "Users" ("Id") VALUES (1),(2);
                INSERT INTO "Songs" ("Id","Title","Lookup") VALUES (1,'Legacy favourite','legacy');
                INSERT INTO "PlayLists" ("Id","Owner","Title") VALUES (1,1,'Keep me');
                INSERT INTO "PlaylistSong" ("PlaylistsId","SongsId") VALUES (1,1);
                INSERT INTO "Parties" ("Id","CreatorId","Name","SupportedPlatforms") VALUES (1,1,'Legacy party',1);
                INSERT INTO "PartySongs" ("Id","PartyId","SongId","PlayedTimes") VALUES (1,1,1,2),(2,1,1,3),(3,1,1,4);
                INSERT INTO "PartySongUser1" ("UpVotersId","UpvotesId") VALUES (1,1),(1,2),(2,3);
                INSERT INTO "PartySongUser" ("DownVotersId","DownvotesId") VALUES (2,2);
                """);
            await SchemaUpgrade.Apply(db);
            await SchemaUpgrade.Apply(db);
            var queue = await db.PartySongs.Include(s => s.UpVoters).Include(s => s.DownVoters).SingleAsync();
            Assert.Equal(9, queue.PlayedTimes);
            Assert.Equal(2, queue.UpVoters.Count);
            Assert.Single(queue.DownVoters);
            Assert.Single(await db.Songs.ToListAsync());
            Assert.Single((await db.PlayLists.Include(p => p.Songs).SingleAsync()).Songs);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        } finally {
            NpgsqlConnection.ClearAllPools();
            var suffix = environment == "SV_TEST_COCKROACH" ? "CASCADE" : "WITH (FORCE)";
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" {suffix}", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
