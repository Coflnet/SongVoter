# Verification

Unit tests: `dotnet test tests/SongVoter.Tests --filter Category=Unit`.

The integration suite needs PostgreSQL 18 and CockroachDB 25.2 with permission to
create temporary databases. Set `SV_TEST_DATABASE` and `SV_TEST_COCKROACH` to
**test** connections, then run `dotnet test tests/SongVoter.Tests`. CI starts both
servers. Each run creates a randomly named
database, applies all migrations, and removes only that database on completion.
The legacy upgrade checks duplicate queue entries, preserved votes/favourites/play
counts, and repeated startup against both database engines.

The test uses real authentication, HTTP controllers, transactions, and PostgreSQL.
External catalog metadata is deterministic; no Google or Spotify credentials are
required. It covers saved anonymous identity, proof replay rejection, private
playlists, invite joins, source imports, weighted voting, unsupported sources,
host-only playback, stale/concurrent callbacks, leaving/ending, and persistence.
Native Spotify authorization and actual YouTube/Spotify playback require device
verification separately; passing these tests does not assert that provider
credentials or account entitlements work.
