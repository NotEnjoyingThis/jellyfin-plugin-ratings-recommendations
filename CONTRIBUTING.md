# Contributing

Thanks for helping! Issues and pull requests are welcome in English or Italian.

## Requirements

- .NET SDK 9 and 10
- Docker with Docker Compose (for the test servers)
- PowerShell: Windows PowerShell 5.1 or PowerShell 7 (`pwsh` on Linux/macOS) for the test-server scripts
- ffmpeg (to generate the sample library)

## Build and test

```bash
dotnet build Jellyfin.Plugin.RatingsRecommendations.sln -c Release   # builds net9.0 (Jellyfin 10.11) and net10.0 (Jellyfin 12)
dotnet test Jellyfin.Plugin.RatingsRecommendations.sln -c Release
dotnet format Jellyfin.Plugin.RatingsRecommendations.sln --verify-no-changes
./build.ps1   # or ./build.sh: zips in artifacts/
```

Analyzer warnings are errors. The engine (`src/RatingsRecs.Engine`) must not reference any Jellyfin package; a test checks it.

## Test servers

Two throw-away Jellyfin servers bound to `127.0.0.1` only, with a small fictional library:

```powershell
.\docker\make-sample-library.ps1          # 12 invented movies + 2 series, 5-second clips
.\docker\setup-server.ps1 -Server 10.11   # http://localhost:8811
.\docker\setup-server.ps1 -Server 12      # http://localhost:8812  ($env:JF12_TAG='12.2' for another 12.x)
.\docker\deploy.ps1 -Server all           # build and install the plugin, restart the servers
.\docker\status.ps1                       # plugin status on both servers
.\docker\test-api.ps1                     # end-to-end API checks on both servers (-SkipRestart to go faster)
```

Test credentials are in `docker/.env.example`; copy it to `docker/.env` (git-ignored) to change them.
Server state lives in `docker/.data/` (git-ignored); delete it to start from scratch.

If Windows refuses to run the scripts, allow local scripts for your user once:

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

## Conventions

- Code, identifiers and comments in English; UI strings in English **and** Italian.
- Version-specific code (Jellyfin 10.11 vs 12) only behind the internal gateways in `Compat/` (`#if JF12`).
- No outgoing network calls; no new dependencies without discussing them in an issue first.
- Commits follow [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `test:`, `docs:`, `chore:`.
- Update `CHANGELOG.md` under `Unreleased`.

## Translations

UI strings live in `src/Jellyfin.Plugin.RatingsRecs/Web/i18n/`:

- `<lang>.json` — web client (thumbs, "For you" page, home rows);
- `config-<lang>.json` — settings page (keep the dash: a name like `config.it.json` would become a satellite assembly).

To add a language, copy the two English files, translate the values (not the keys), add the two-letter code to
`WebAssets.Languages` and open a pull request. A test checks that every language has the same keys as English.
Status messages on the settings page come from the server log and stay in English.

## Releases (maintainers)

1. On `main`: rename `## [Unreleased]` in `CHANGELOG.md` to `## [X.Y.Z] - YYYY-MM-DD` (and start a new empty
   `Unreleased`); for the first release also remove the "in development" note at the top of both READMEs. Commit and push.
2. Push the tag: `git tag vX.Y.Z && git push origin vX.Y.Z`.

`.github/workflows/release.yml` then checks that the tag is on `main` and that the CHANGELOG section exists, runs the
tests, builds both zips (version `X.Y.Z.0`, reproducible), publishes the GitHub Release with the zips and their
`.sha256` files, adds the two new entries on top of `manifest.json` (Jellyfin 12 first) with a bot commit on `main`,
and finally downloads the zips again to check their MD5 against the manifest. Never edit `manifest.json` by hand.
The workflow needs permission to push to `main` (allow GitHub Actions in the branch protection rules, if any).

The helpers are in `scripts/release.mjs` (Node, no dependencies); `node --test scripts/release.test.mjs` runs their tests.
