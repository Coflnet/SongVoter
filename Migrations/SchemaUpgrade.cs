using System;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.SongVoter.DBModels;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.SongVoter.Migrations;

public static class SchemaUpgrade
{
    public static async Task Apply(SVContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var version = db.Database.GetDbConnection().CreateCommand();
        version.CommandText = "SELECT version()";
        if (!((string)await version.ExecuteScalarAsync()).StartsWith("CockroachDB", StringComparison.Ordinal)) {
            await db.Database.MigrateAsync();
            return;
        }

        // CockroachDB cannot run EF's PostgreSQL LOCK TABLE or transactional DDL.
        // These resumable statements upgrade the existing service; new databases use PostgreSQL.
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToHashSet();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pending.Length == 0) return;
        string[] supported = ["20260919004235_GuestProfiles", "20260919005004_WeightedParties"];
        if (!applied.Contains("20230829170906_AddLookup") || pending.Except(supported).Any())
            throw new InvalidOperationException("Review the CockroachDB upgrade for these migrations before deployment: " + string.Join(", ", pending));

        string[] schema = [
            "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"DeviceKeyHash\" VARCHAR(64) NULL",
            "CREATE TABLE IF NOT EXISTS \"AuthChallenges\" (\"Id\" VARCHAR(64) PRIMARY KEY, \"IdentityHash\" VARCHAR(64), \"Difficulty\" INTEGER NOT NULL, \"ExpiresAt\" TIMESTAMPTZ NOT NULL)",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Users_DeviceKeyHash\" ON \"Users\" (\"DeviceKeyHash\")",
            "CREATE INDEX IF NOT EXISTS \"IX_AuthChallenges_ExpiresAt\" ON \"AuthChallenges\" (\"ExpiresAt\")",
            "ALTER TABLE \"PartySongs\" ALTER COLUMN \"PlayedTimes\" TYPE INT4",
            "ALTER TABLE \"Parties\" ADD COLUMN IF NOT EXISTS \"CurrentSongId\" INTEGER NULL",
            "ALTER TABLE \"Parties\" ADD COLUMN IF NOT EXISTS \"PlaybackVersion\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Invites\" ADD COLUMN IF NOT EXISTS \"Code\" VARCHAR(12) NULL"
        ];
        foreach (var statement in schema) await db.Database.ExecuteSqlRawAsync(statement);
        await using (var transaction = await db.Database.BeginTransactionAsync()) {
            await db.Database.ExecuteSqlRawAsync(LegacyQueue.MergeDuplicates);
            await transaction.CommitAsync();
        }
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PartySongs_PartyId_SongId\" ON \"PartySongs\" (\"PartyId\", \"SongId\")");
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Invites_Code\" ON \"Invites\" (\"Code\")");
        foreach (var migration in supported)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({migration}, '10.0.12') ON CONFLICT DO NOTHING");
    }
}
