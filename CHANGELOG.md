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
