# Ratings & Recommendations for Jellyfin

[![Latest release](https://img.shields.io/github/v/release/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations)](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/releases)
[![CI](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/actions/workflows/ci.yml/badge.svg)](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/actions/workflows/ci.yml)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11.9%2B%20%7C%2012.0%2B-00a4dc)
[![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue)](LICENSE)

*[Leggi in italiano](README.it.md)*

> **Status: in development.** The first public release is v1.0.0. Until then the catalog has no installable version.

Replace the Favourite heart with a 4-level rating — **Hate it (-3)**, **Watchable (-1)**, **Good (+1)**, **Amazing (+3)** —
and get recommendations built from what you liked *and* what you disliked. Everything is computed on your server.

**Requires Jellyfin 10.11.9 or later, or Jellyfin 12.0 or later.**

## Features

- 4-level thumbs selector in the web client instead of the heart (detail pages and card overlays).
- Ratings mirrored onto Jellyfin's own `Likes`, `Rating` and Favourite fields, so they survive an uninstall.
- Personal, content-based recommendations (genres, directors, actors, writers, tags, studios, decades, collections),
  with explanations such as "Because you loved *X*".
- "Recommended for you" and "Rate these" home rows (via Home Screen Sections, or added by the plugin script),
  a "For you" page, and a "Similar, picked for you" row on detail pages.
- Jellyfin 12: native similar-items provider, so recommendations also reach TV and mobile apps.
- Optional per-season ratings for TV series.
- Imports existing Favourites as +3; CSV export/import of ratings.

## Compatibility

| Jellyfin | Plugin build | Notes |
|---|---|---|
| 10.11.0 – 10.11.8 | not supported | upgrade Jellyfin to 10.11.9 or later |
| 10.11.9 – 10.11.x | `jf10.11` (targetAbi 10.11.9.0) | tested on 10.11.11 |
| 12.0 and later | `jf12` (targetAbi 12.0.0.0) | tested on 12.4 |

Jellyfin picks the right build automatically from the repository.

## Installation

1. **Dashboard → Plugins → Repositories** (on 10.11: **Catalog** → settings icon) → **+**.
   Name `Ratings & Recommendations`, URL:
   ```
   https://raw.githubusercontent.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/main/manifest.json
   ```
2. Add the repository of **File Transformation** (needed for the thumbs in the web client) and, if you want the home rows,
   **Home Screen Sections**. Both (and Plugin Pages) are in the same repository:
   ```
   https://www.iamparadox.dev/jellyfin/plugins/manifest.json
   ```
3. **Catalog** → install **File Transformation**, then **Ratings & Recommendations** (optionally **Home Screen Sections**) → restart Jellyfin.
4. Open **Dashboard → Plugins → Ratings & Recommendations**: the settings page shows whether the dependencies are found.
5. Reload the web client once with **Ctrl+F5** (the browser may keep the old page in its cache).

Updates appear in the dashboard automatically.

**Without File Transformation** the API, the recommendations and the Jellyfin 12 provider still work; the settings page
shows how to add the script manually.

## Home rows and the "For you" page

- **With Home Screen Sections**: the administrator enables Home Screen Sections in its own settings; each user then opens
  *Modular Home* (side menu), enables it and ticks **Recommended for you** and **Rate these**, choosing their position.
- **Without Home Screen Sections** (or when it is not drawing the home): the plugin script adds both rows at the top of
  the home by itself. "Rate these" disappears after 15 ratings (configurable).
- The **For you** page (Movies / Series, with explanations) opens from the side menu, from the row titles, or from
  Plugin Pages if installed. Movie and series pages get a **Similar, picked for you** row under "More like this".

## Configuration

Dashboard → Plugins → Ratings & Recommendations. Groups: Ratings (names and values of the levels, Favourite behaviour,
implicit signals, series rating mode), Engine (category weights, smoothing, half-life, score coefficients, diversity),
Delivery (home rows, Jellyfin 12 provider, 10.11 suggestions override), Tools (recompute, import Favourites, CSV, preview).

### Per-season ratings: check your metadata first

The "Per season" and "Both" modes only give better results if your seasons have **per-episode credits** (directors,
writers, guest stars). With poor metadata — common with ripped DVDs named by hand — they behave almost like
"Whole series". Refresh your TV metadata from TMDb or TVDb before enabling them; the settings page shows the share of
seasons without per-episode credits.

Switching mode never deletes ratings: the ratings the current mode does not use are kept and count again if you switch
back. If users may choose, each user picks a mode on the "For you" page. For anthology series (each season stands
alone) add the tag `anthology` (or `antologia`) to the series in the metadata editor: recommendations then suggest
starting from the best-matching season instead of season 1.

## Privacy

The plugin makes **no outgoing network calls** and collects **no usage statistics**. Ratings stay in the plugin's data
folder on your server (one JSON file per user) and in Jellyfin's own database.

## Migrating from Jellyfin 10.11 to 12

Jellyfin recommends removing third-party plugins before a major upgrade:

1. Settings page → **Export CSV** (backup of all ratings).
2. Upgrade Jellyfin to 12, then install the plugin from the catalog (the `jf12` build is chosen automatically).
3. Settings page → **Import CSV**.

Ratings are also mirrored onto Jellyfin's `Likes`/`Rating` fields, which survive the upgrade.

## FAQ

**The thumbs do not appear.** Check that File Transformation is installed and active, then reload the page with
Ctrl+F5. Open the browser console (F12) and look for lines starting with `[RatingsRecs]`.

**Do TV/mobile apps show the thumbs?** No: they keep the heart, which counts as +3. On Jellyfin 12 the "Similar" lists
in those apps use your ratings.

**Can other users see my ratings?** No. Every user has separate ratings and recommendations; only administrators can
export other users' ratings.

## Building locally

Requirements: .NET SDK 9 and 10.

```bash
dotnet test Jellyfin.Plugin.RatingsRecommendations.sln -c Release
./build.ps1   # Windows PowerShell 5.1 / PowerShell 7
./build.sh    # Linux/macOS (needs zip)
```

The zips land in `artifacts/`. See [CONTRIBUTING.md](CONTRIBUTING.md) for the Docker test servers.

## License

[GPL-3.0](LICENSE). Design document: [docs/DESIGN.md](docs/DESIGN.md).
