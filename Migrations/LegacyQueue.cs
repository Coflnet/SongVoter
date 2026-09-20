namespace Coflnet.SongVoter.Migrations;

internal static class LegacyQueue
{
    // Preserve each person's vote and the combined play count before enforcing uniqueness.
    internal const string MergeDuplicates = """
        INSERT INTO "PartySongUser1" ("UpVotersId", "UpvotesId")
        SELECT votes."UpVotersId", canonical.id
        FROM "PartySongUser1" votes JOIN "PartySongs" song ON song."Id" = votes."UpvotesId"
        JOIN (SELECT "PartyId", "SongId", MIN("Id") id FROM "PartySongs" GROUP BY "PartyId", "SongId") canonical
          ON canonical."PartyId" = song."PartyId" AND canonical."SongId" = song."SongId"
        ON CONFLICT DO NOTHING;
        INSERT INTO "PartySongUser" ("DownVotersId", "DownvotesId")
        SELECT votes."DownVotersId", canonical.id
        FROM "PartySongUser" votes JOIN "PartySongs" song ON song."Id" = votes."DownvotesId"
        JOIN (SELECT "PartyId", "SongId", MIN("Id") id FROM "PartySongs" GROUP BY "PartyId", "SongId") canonical
          ON canonical."PartyId" = song."PartyId" AND canonical."SongId" = song."SongId"
        ON CONFLICT DO NOTHING;
        UPDATE "PartySongs" song SET "PlayedTimes" = canonical.plays
        FROM (SELECT MIN("Id") id, SUM("PlayedTimes") plays FROM "PartySongs" GROUP BY "PartyId", "SongId") canonical
        WHERE song."Id" = canonical.id;
        DELETE FROM "PartySongs" WHERE "Id" NOT IN (SELECT MIN("Id") FROM "PartySongs" GROUP BY "PartyId", "SongId");
        """;
}
