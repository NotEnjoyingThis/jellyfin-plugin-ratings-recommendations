# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) (plugin version `X.Y.Z.0`).

The section of each version is used for the GitHub Release notes and the `changelog` field of `manifest.json`.

## [Unreleased]

### Added
- Project skeleton: engine and plugin projects, dual build for Jellyfin 10.11.9+ (net9.0) and 12.0+ (net10.0).
- Settings page with plugin version and dependency status (File Transformation, Home Screen Sections, Plugin Pages).
- Packaging scripts producing one zip per Jellyfin line with MD5 and SHA-256.
- Docker test servers (10.11.11 and 12.4) with a fictional sample library.
- Rating store: one JSON file per user with schema version, atomic writes and per-user locking; kept across plugin updates.
- Ratings API: `GET/PUT/DELETE /RatingsRecs/Ratings/{itemId}`, bulk `GET /RatingsRecs/Ratings?itemIds=`,
  `GET /RatingsRecs/Export`, `POST /RatingsRecs/Import` (CSV), `POST /RatingsRecs/ImportFavorites`.
- Ratings mirrored onto Jellyfin's `Rating`/`Likes` and Favourites as in the design table.
- A Favourite set by a client without thumbs counts as an implicit +3 (explicit ratings always win).
- Settings page: rating names/values, Favourite options, implicit signal weights; tools to import Favourites and export/import CSV.
- Web client: the Favourite heart is replaced (hidden, not removed) by a 4-level thumbs selector on movie and series
  detail pages and on card overlays, in the Legacy and Modern layouts; keyboard and screen-reader friendly,
  optimistic saving with rollback and an error message; theme colours; Italian and English texts.
- Script injection through File Transformation (registered by reflection); without it the hook switches itself off
  and the settings page shows how to add the script manually.
- `GET /RatingsRecs/Status` (administrators) with the state of the integration hooks.
- Recommendation engine (content-based): taste profile with IDF, smoothing and time decay; taste + quality + daily
  exploration score; exclusions, proximity penalty, MMR diversity, saga cap, explanations, cold start and "Rate these".
- Background computation with in-memory cache: scheduled task every 6 hours, recomputation 30 s after a rating change
  and after library scans; `GET /RatingsRecs/Recommendations` and `GET /RatingsRecs/Onboarding` answer from the cache.
- Implicit signals: watched without rating (+0.5), abandoned (-0.5).
- "For you" page (`#/ratingsrecs`) with Movies/Series tabs, explanations and thumbs on every card.
- Settings page: engine coefficients, "Recompute now", recommendations preview for a user.
- TV series rating modes: Whole series (default), Per season, Both; server default plus optional per-user choice on the
  "For you" page; ratings are never deleted when the mode changes.
- Per-season ratings: thumbs on season pages and on season cards; "copy the series rating to the unrated seasons".
- Season features aggregated from episodes (directors, writers, top 5 guest stars, decade, average rating); seasons
  without per-episode credits fall back to the series. Series score = 0.6 × best season + 0.4 × mean (configurable),
  season-to-season explanations, "start from season N" for anthologies (tag "anthology"/"antologia").
- Settings page: series mode options and the share of seasons without per-episode credits.
- Home Screen Sections: "Recommended for you" and "Rate these" sections registered for users to add from Modular Home.
- Home rows drawn by the web script when the home is not rendered by Home Screen Sections.
- "For you" entry in the side menu (Legacy and Modern) and in Plugin Pages; clickable row titles.
- "Similar, picked for you" row on movie and series pages (`GET /RatingsRecs/Similar/{itemId}`), from the cached profile.
- Settings page: delivery options (home rows mode and size, similar row, menu entry).

### Fixed
- The web script was not injected when another plugin (Home Screen Sections, Plugin Pages) also transformed `index.html`.
- "For you" was unreachable in the Jellyfin 12 Modern layout on wide windows: it now has a top-bar button and an
  avatar-menu entry; the Plugin Pages entry is shown in the user's language and is not duplicated in the side menu.
- The "Recommended for you" and "Rate these" row titles now open the matching section of the "For you" page
  (`#/ratingsrecs?section=recommended|rate`) instead of its top.
- The Legacy header showed "Page not found" on the "For you" page.
- README: how to fix "Page not found" for Plugin Pages entries (stale cached web client after a Jellyfin start).
