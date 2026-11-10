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
