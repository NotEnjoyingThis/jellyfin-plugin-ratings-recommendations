# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) (plugin version `X.Y.Z.0`).

The section of each version is used for the GitHub Release notes and the `changelog` field of `manifest.json`.

## [Unreleased]

## [1.0.0] - 2026-11-11

First release. Requires Jellyfin 10.11.9+ or 12.0+.

### Added
- 4-level rating (Hate it -3, Watchable -1, Good +1, Amazing +3) instead of the Favourite heart in the web client,
  on detail pages and cards, in the Legacy and Modern layouts (needs File Transformation).
- Ratings mirrored onto Jellyfin's `Rating`/`Likes` and Favourites; a heart set in other apps counts as +3.
- Local, content-based recommendations from what you liked and disliked (genres, directors, actors, writers, tags,
  studios, decades, collections), with explanations, diversity and a cold start with "Rate these".
- "For you" page, "Recommended for you" and "Rate these" home rows (Home Screen Sections or the plugin script) and a
  "Similar, picked for you" row on detail pages.
- TV series rating modes: whole series, per season or both, with per-season features from the episodes' credits.
- Jellyfin 12: native "More like this" provider ranked with each user's taste (also in TV and mobile apps), per library.
- Jellyfin 10.11: optional override of Jellyfin's suggestions (off by default).
- Settings page in English and Italian: rating names and values, engine weights, delivery options, status of every
  integration, startup compatibility check; tools to import Favourites, export/import ratings as CSV, recompute and
  preview recommendations.
- REST API under `/RatingsRecs` (ratings, recommendations, onboarding, similar items, preferences, CSV).
