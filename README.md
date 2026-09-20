# SongVoter

A collaborative automatic DJ: guests scan a QR at **songvoter.party**, choose
favourite songs, and the host's Android app plays the room's queue through
YouTube or Spotify. The shared Flutter client is in `../song_voter`; deployment
is in `../fleet/songvoter`.

Guests never need a manual sign-in. The client saves a random 256-bit device
secret and solves a short, expiring SHA-256 challenge in the background. The API
stores only the secret's hash, atomically consumes each challenge, and issues a
one-day JWT. Refreshing uses the same device identity, so favourites and event
membership survive browser/app restarts. Old nonce-only authentication and the
public database administration endpoints have been removed.

Each profile owns its favourites (up to 30), and those favourites join an event
with the guest. Repeated additions are idempotent. Each anonymous participant
has a total weight of 1; the host or a verified Google profile has weight 3.
Weight is divided across that participant's votes, so a longer list cannot
multiply their influence. Unplayed songs go first, then score, then stable
insertion order. Played tracks rotate back in, avoiding an immediate repeat
when another eligible song exists. Guests who leave stop influencing the queue.
Proof of work and rate limits increase the cost of spam; they do not prove that
multiple profiles belong to different people.

Only the event owner can advance playback, end the event, refresh an invite, or
remove a participant. Advancing claims a database playback version before
updating the current track, so duplicated/delayed SDK callbacks cannot skip
songs. Unconnected sources remain visible and are skipped by playback.

## Run

Requires .NET 10 and a PostgreSQL-compatible database (integration tests use
PostgreSQL 18). Configure `DB_CONNECTION`, `jwt__secret` (random, at least 32
bytes), and `hashids__salt`; copy `appsettings.example.json` for other settings.
The old sample JWT secret is refused. Local `appsettings.json` may contain
private credentials and is excluded from container builds.

```
dotnet restore --locked-mode
dotnet run -- --migrate-only
dotnet run
```

The existing CockroachDB service uses a separate, resumable upgrade in
`Migrations/SchemaUpgrade.cs`: CockroachDB does not support EF's PostgreSQL
migration lock or transactional schema changes. It upgrades the known 2023
schema, preserving favourites and consolidating duplicate queue votes/play
counts before adding uniqueness. Back up the database first. Review and extend
this explicit path when adding a migration; unknown migrations stop deployment.
New installations use PostgreSQL and standard EF migrations.

The API listens on port 4200 (override with `ASPNETCORE_URLS`). Swagger is at
`/api/docs`. `/status` checks database readiness, including an empty database;
`/health/live` is the process liveness check. For an isolated local database,
set `SONGVOTER_JWT_SECRET` and `SONGVOTER_ID_SALT`, then run:

```
docker compose run --build --rm songvoter --migrate-only
docker compose up -d
```

## Configuration and deployment

YouTube metadata needs `youtube__apiKey`. Spotify metadata uses
`spotify__clientid` and `spotify__clientsecret`. Native playback uses official
provider players on the host, with the Spotify app's own authorization; no
Spotify client secret goes to the frontend. Configure the native Spotify
redirect and signing fingerprint as described in the client README.

The existing OpenBao provider accepts `OPENBAO__ADDR`, `OPENBAO__AUTH_PATH`,
`OPENBAO__ROLE`, `OPENBAO__PATH`, `OPENBAO__TOKEN_PATH`, and
`OPENBAO__CACERT`. Fleet uses a projected `openbao` audience token and verifies
OpenBao TLS using its CA. Secret loading is mandatory in production. Keep JWT
signing configuration stable across replicas; the Fleet deployment loads it at
startup. Secret rotation requires a coordinated rollout.

CI follows `../hypixel`: the reusable Coflnet container workflow builds, scans,
and requests OIDC-verified Argo promotion into `Coflnet/fleet`. The integration
suite gates image publication. Fleet activation requires successful image
promotion and the secret/database preflight in its README; GitOps owns rollout.
Configure only observed trusted proxy networks so authentication rate limits
apply to client IPs. Limits are per process; add shared enforcement before
scaling beyond the configured single replica.

## Extending and future priority boosts

`IMusicCatalog` resolves/searches metadata for one platform. `SongCatalog`
normalizes supported share links and stores source identities without guessing
that similarly named recordings are identical. One provider outage does not
hide results from another. Add a catalog and Flutter `PlaybackAdapter` to
support another platform.

Everything is free. `paidPriorityEnabled` is false, and no client-supplied
priority or payment endpoint exists. Future boosts belong in the server's queue
score, awarded through verified, idempotent payment events after reviewing the
provider terms. Spotify restricts monetization of playback integrations:
[Spotify compliance guidance](https://developer.spotify.com/compliance-tips).

## Verification

See [tests/README.md](tests/README.md) for .NET tests and
[tests/browser/README.md](tests/browser/README.md) for Chromium tests.
The client has its own Flutter test suite. Real provider authorization and
playback require an Android/iOS device; iOS builds additionally need macOS/Xcode.
