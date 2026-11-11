# Ratings & Recommendations for Jellyfin

[![Latest release](https://img.shields.io/github/v/release/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations)](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/releases)
[![CI](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/actions/workflows/ci.yml/badge.svg)](https://github.com/NotEnjoyingThis/jellyfin-plugin-ratings-recommendations/actions/workflows/ci.yml)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11.9%2B%20%7C%2012.0%2B-00a4dc)
[![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue)](LICENSE)

*[Leggi in italiano](README.it.md)*

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
- Jellyfin 10.11: optional override of Jellyfin's suggestions (off by default).
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

### With Home Screen Sections

Install **Home Screen Sections** and **Plugin Pages** (Plugin Pages adds the *Modular Home* entry users need), then:

1. **Administrator:** Dashboard → Plugins → **Home Screen Sections** → *Global Settings*:
   - **Enabled by Default** turns the modular home on for every user (it is off after installation, so otherwise each
     user has to switch it on);
   - **Allow User Override** (on by default) lets users switch it on or off and pick their sections.

   The *Section Settings* tab chooses which sections exist, their default order and whether users may change each one
   ("Let the user override this section"). Save; reload the web client with Ctrl+F5.
2. **Each user** (needed when *Enabled by Default* is off, or to choose sections): open **Modular Home**:
   - **Legacy layout** (Jellyfin 10.11, or 12 with Settings → Display → Layout "Desktop (legacy)"): side menu;
   - **Modern layout** (Jellyfin 12 default): the avatar menu at the top right (on a phone: side menu ☰).
3. Tick **Enable "Modular Home"**, then under **Enabled Sections** tick **Recommended for you** and **Rate these**
   (and the other sections you want) → **Save** → go back to the home.

### Without Home Screen Sections

The plugin script adds both rows at the top of the home by itself (also when Home Screen Sections is installed but not
enabled). "Rate these" disappears after 15 ratings (configurable).

### Where to find the "For you" page

| Layout | Entry point |
|---|---|
| Legacy (10.11, 12 "Desktop (legacy)") | side menu, under Home |
| Modern, wide window (12 default) | top bar, before the libraries, and the avatar menu |
| Modern, narrow window / phone | side menu (☰), under Home |

The row titles open it too, scrolled to the matching section (`#/ratingsrecs?section=recommended` or
`?section=rate`). Movie and series pages get a **Similar, picked for you** row under "More like this".

## Jellyfin 12: native "More like this"

On Jellyfin 12 the plugin can rank the native "More like this" lists of movies and series with each user's taste. These
lists are also shown by the TV and mobile apps.

1. Dashboard → Plugins → Ratings & Recommendations → **Delivery** → tick the libraries (movies, shows or mixed) → Save.
2. The plugin puts its provider first in the "similar items" provider order of those libraries (Dashboard → Libraries →
   library → similar item providers) and removes it from the others. The settings page shows where it is active.
3. To turn it off, untick the library and save: Jellyfin's own lists come back. **Untick everything and save before
   uninstalling the plugin.**

Limits:
- Items rated negatively are not shown. In a library with fewer titles than the list asks for, they come last instead of
  being left out, because Jellyfin would add them back in arbitrary positions.
- Jellyfin's movie recommendations ("Because you watched…") use only Jellyfin's own provider and are not changed.
- Jellyfin 10.11 has no such provider: the plugin's own "Similar, picked for you" row works on both versions.

## Jellyfin 10.11: suggestions (optional)

Jellyfin 10.11 has no similar-items provider, so the TV and mobile apps keep Jellyfin's own suggestions. As an
**optional, off-by-default** setting (Delivery → "Jellyfin 10.11: suggestions") the plugin can change two of Jellyfin's
lists for every client:

- `/Items/Suggestions` for movies and series → replaced with the user's recommendations;
- `/Movies/Recommendations` ("Because you watched…") → each list reordered with the user's taste, without the titles
  rated negatively.

It works by changing Jellyfin's responses, so it depends on Jellyfin internals: if anything goes wrong it switches
itself off, logs the error and shows it on the settings page. On Jellyfin 12 use the native provider above instead.

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

**"Modular Home" (or another Plugin Pages entry) shows "Page not found", and the address is
`#/userpluginsettings?pageUrl=…` without `.html`.** This is not caused by this plugin. Your browser is running an old
copy of `main.jellyfin.bundle.js` without the page that Plugin Pages adds (`userpluginsettings.html`), and the web
client rewrites unknown `….html` addresses without `.html`. For a few seconds after every Jellyfin start (and before
Plugin Pages is installed) that file is served unmodified and **without `Cache-Control`**: the browser may then keep
it for a day or more without asking the server again, so a normal reload does not help and neither does adding `.html`.
Fix, once Jellyfin has fully started:
- Chrome / Edge: open the developer tools (F12), right-click the reload button → **Empty cache and hard reload**;
- or clear the browser's site data for your Jellyfin address (you will have to sign in again);
- or, in the developer tools console on the Jellyfin page, run:
  ```js
  fetch([...document.scripts].find(s => /main\.jellyfin\.bundle/.test(s.src)).src, { cache: 'reload' }).then(() => location.reload())
  ```
Avoid opening the web client in the first seconds after restarting Jellyfin.

**After an update the settings page looks old or shows untranslated keys.** The browser kept the previous page:
reload the dashboard with Ctrl+F5.

**The settings page says "This is the build for Jellyfin 10.11 but the server runs 12".** A zip for the other Jellyfin
version was installed by hand: remove the plugin and install it from the catalog, which picks the right build.

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
