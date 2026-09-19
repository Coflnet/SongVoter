# Verification

Unit tests: `dotnet test tests/SongVoter.Tests --filter Category=Unit`.

The full HTTP integration test needs PostgreSQL 18 with permission to create a
temporary database. Set `SV_TEST_DATABASE` to a **test** PostgreSQL connection,
then run `dotnet test tests/SongVoter.Tests`. Each run creates a randomly named
database, applies all migrations, and removes only that database on completion.

The test uses real authentication, HTTP controllers, transactions, and PostgreSQL.
External catalog metadata is deterministic; no Google or Spotify credentials are
required. It covers saved anonymous identity, proof replay rejection, private
playlists, invite joins, source imports, weighted voting, unsupported sources,
host-only playback, stale/concurrent callbacks, leaving/ending, and persistence.
Native Spotify authorization and actual YouTube/Spotify playback require device
verification separately; passing these tests does not assert that provider
credentials or account entitlements work.
