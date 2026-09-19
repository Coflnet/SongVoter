# Browser end-to-end tests

These tests run the **built Flutter website** in Chromium against a real local
API/PostgreSQL instance. They perform a QR join, automatic proof-of-work login,
song search, adding a favourite, host playback advance, polling updates, reload
persistence, leaving, and recovery from an invalid invite. Both mobile and
desktop screenshots are saved under `test-results`.

Run `./run.sh` from this directory. It requires Docker, .NET 10, Flutter 3.47.5,
Node, and Chromium. It creates only a dedicated test database container; cleanup
removes that container, and does not touch any existing database. Set
`FLUTTER_BIN` if Flutter is not on PATH. Existing built web assets can be reused
with `SKIP_WEB_BUILD=1`. Override `CHROMIUM_PATH` for another Chromium binary.
Ports default to 4208/4308; override `API_PORT`/`WEB_PORT` when needed.

Set `ANDROID_DEVICE=emulator-5560` to also run the native host integration test
on an already running, disposable Android emulator. `LIVE_PLAYBACK=true` adds
real YouTube start/pause/resume/end-event checks and needs unrestricted provider
access. The emulator reaches the local API through `10.0.2.2`. The test covers
party creation, QR contents and visibility in both orientations, and ending the
event. Use only a disposable emulator; it stores a test guest profile.

Metadata is seeded for two songs; provider credentials are deliberately empty.
This verifies the application flow without consuming provider quotas and does
not claim audible Spotify/YouTube playback. Backend tests separately cover both
imports; Flutter tests cover provider switching and SDK end-event coordination.
