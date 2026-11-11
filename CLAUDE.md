# CLAUDE.md — Ratings & Recommendations (Jellyfin plugin)

## Goal
Jellyfin server plugin that replaces the Favourite heart in the web client with a 4-level rating
(-3 Detesto, -1 Guardabile, +1 Bello, +3 Fantastico), mirrors ratings onto native `UserItemData`
(`Likes`, `Rating`, `IsFavorite`) and computes local, content-based recommendations per user.

**The specification is [docs/DESIGN.md](docs/DESIGN.md).** If anything here conflicts with it, the design wins;
deliberate deviations are listed in [docs/NOTES.md](docs/NOTES.md) → "Deviazioni dal design" (keep that section updated
at every milestone). API findings (10.11 vs 12 signatures, selectors) also go in docs/NOTES.md.

## Fixed identifiers — NEVER change
- Plugin GUID: **`4d56ae7e-38cb-431c-bff8-e135c2087aa5`** (Plugin.cs, build.yaml, meta.json, manifest.json, configPage.html, tests)
- Assembly / technical name: `Jellyfin.Plugin.RatingsRecs`; catalog name "Ratings & Recommendations"
- Repository: https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations (GPL-3.0)
- Manifest URL: https://raw.githubusercontent.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/main/manifest.json

## Builds
| Build | TFM | Jellyfin packages (pinned, compile-only) | targetAbi | Symbol |
|---|---|---|---|---|
| jf10.11 | net9.0 | 10.11.9 | 10.11.9.0 | `JF10_11` |
| jf12 | net10.0 | 12.0.0 | 12.0.0.0 | `JF12` |

- Package versions, targetAbi and symbols live in `Directory.Build.targets`; plugin version in `build.yaml` (4 numbers, same for both builds).
- `#if JF12` only inside `Compat/` (BuildInfo, `ILibraryGateway`, `IUserDataGateway`, `VersionServices`) and JF12-only files (`Similar/RatingsSimilarItemsProvider.cs`, `Similar/SimilarProviderSync.cs`). Nowhere else.
- `src/RatingsRecs.Engine` has NO Jellyfin dependency; the plugin compiles its sources in (single DLL in the zip). A test enforces this.

## Commands
```bash
dotnet build Jellyfin.Plugin.RatingsRecommendations.sln -c Release      # both TFMs
dotnet test Jellyfin.Plugin.RatingsRecommendations.sln -c Release
dotnet format Jellyfin.Plugin.RatingsRecommendations.sln --verify-no-changes
./build.ps1            # Windows (PS 5.1 / 7): artifacts/ratings-recommendations_<ver>_jf10.11.zip / _jf12.zip + .md5/.sha256
./build.sh             # Linux/macOS/CI (needs zip)
```
Test servers (Docker, bound to 127.0.0.1; credentials in `docker/.env.example`, override in git-ignored `docker/.env`):
```powershell
.\docker\make-sample-library.ps1          # fictional 12 movies + 2 series (ffmpeg + .nfo)
.\docker\setup-server.ps1 -Server 10.11   # http://localhost:8811 (jellyfin/jellyfin:10.11.11)
.\docker\setup-server.ps1 -Server 12      # http://localhost:8812 (jellyfin/jellyfin:12.4; JF12_TAG=12.2 to change)
.\docker\install-deps.ps1 -Server all     # File Transformation (+ -Plugins 'Plugin Pages','Home Screen Sections')
.\docker\deploy.ps1 -Server all           # build + copy into config/plugins/RatingsRecs_<ver> + restart
.\docker\status.ps1                       # plugin version/status, config page, log errors
.\docker\test-api.ps1                     # end-to-end API checks on both servers (-SkipRestart to go faster)
docker compose -f docker/compose.10.11.yml down   # stop (state kept in docker/.data)
```
Docker helper scripts are PowerShell only (use `pwsh` on Linux/macOS). Keep `.ps1` files ASCII-only and compatible with
Windows PowerShell 5.1 (no `&&`, `??`, ternary; wrap native commands with `Invoke-Native`).

## Layout
```
src/RatingsRecs.Engine/            pure engine (DTO in, ranking + explanations out; deterministic given seed)
src/Jellyfin.Plugin.RatingsRecs/   Plugin, ServiceRegistrator, Compat/, Configuration/configPage.html, Web/ratings.js|css
tests/RatingsRecs.Engine.Tests/    xUnit, engine
tests/RatingsRecs.Plugin.Tests/    xUnit, integration (store, API)
docker/                            compose files + scripts for the test servers
assets/                            logo.png (render-logo.ps1)
.github/                           ci.yml, release.yml (tag vX.Y.Z), issue/PR templates
scripts/                           release.mjs (+ tests: node --test scripts/)
```

## Conventions
- **Always reply to the user in Italian** (code, identifiers, comments and commit messages stay in English).
- Code, identifiers and comments in English; UI strings in Italian and English via translation files.
- Analyzers from the official template (StyleCop, Serilog, Multithreading) with `TreatWarningsAsErrors`.
- Tests first for engine and rating store. Never mark a milestone done unless both builds compile and all tests pass.
- Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`, `chore:`). No push, tag or publish without the user's request.
- No outgoing network calls, no direct DB writes (server APIs only), no heavy work inside HTTP requests.
- Every endpoint authenticated; user from the token (admins may target others); respect library access and parental control.
- No new NuGet/JS dependency without asking. Hooks (web injection, HSS, 12 provider, 10.11 override) must self-disable on failure, log it and show it on the settings page.
- `manifest.json` is only modified by the release pipeline; jf12 entry always before jf10.11 for the same version.

## Current status
- Last milestone committed: M6 (polish). Release v1.0.0 prepared (CHANGELOG section, READMEs): the user pushes `main` and
  the tag `v1.0.0` (see CONTRIBUTING → Releases); then the M6 acceptance test (install from the catalog on clean 10.11
  and 12, v1.0.1 shows as an update).
- M6: 10.11 override (`Override/`, registered only in the 10.11 build), `Compatibility` hook, settings page texts in
  `Web/i18n/config-<lang>.json`, release tooling in `scripts/release.mjs` + `.github/workflows/release.yml`.
- M5 design: per-library switch in the plugin settings (`SimilarProviderLibraryIds`); `SimilarProviderSync` puts our provider
  first in `TypeOptions.SimilarItemProviderOrder` of the selected libraries via `CollectionFolder.UpdateLibraryOptions` and never
  creates missing `TypeOptions`. Limits (small libraries, `/Movies/Recommendations`) in NOTES deviations 34–37.
- Web client checks must always cover **Legacy and Modern** layouts on 12 (Settings → Display → layout: `auto` = Modern,
  `desktop-legacy` = Legacy) plus 10.11.

## Milestones (docs/DESIGN.md §9)
M0 skeleton ✅ → M1 store + API ✅ → M2 web thumbs ✅ → M3 engine ✅ → M3b seasons ✅ → M4 home rows ✅ → M5 JF12 provider ✅ → M6 polish ✅ (release v1.0.0: user's tag).
Stop after each milestone and wait for the user's go-ahead.
