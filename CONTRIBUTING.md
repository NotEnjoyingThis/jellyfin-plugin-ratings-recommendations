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

UI strings live in the plugin's translation files (one per language). To add a language, copy the English file,
translate the values (not the keys) and open a pull request.

## Releases (maintainers)

Update `CHANGELOG.md`, then push a tag `vX.Y.Z`. The release workflow builds both zips, publishes the GitHub Release
and updates `manifest.json`. Never edit `manifest.json` by hand.
