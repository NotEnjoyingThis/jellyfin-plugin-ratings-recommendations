#!/usr/bin/env bash
# Builds the two plugin zips (Jellyfin 10.11 and Jellyfin 12) with MD5 and SHA-256 files.
# Output: artifacts/ratings-recommendations_<version>_jf10.11.zip and _jf12.zip,
# each containing only Jellyfin.Plugin.RatingsRecs.dll and meta.json.
#
# Usage: ./build.sh [--version 1.2.3.0] [--only jf10.11|jf12]
# Requires: dotnet (SDK 9 + 10), zip, md5sum/sha256sum (or md5/shasum on macOS).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARTIFACTS="$ROOT/artifacts"
PROJECT="$ROOT/src/Jellyfin.Plugin.RatingsRecs/Jellyfin.Plugin.RatingsRecs.csproj"
DLL="Jellyfin.Plugin.RatingsRecs.dll"
VERSION=""
ONLY=""

while [ $# -gt 0 ]; do
  case "$1" in
    --version) VERSION="$2"; shift 2 ;;
    --only) ONLY="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

command -v zip >/dev/null 2>&1 || { echo "zip is required (on Windows use build.ps1)" >&2; exit 1; }

YAML="$ROOT/build.yaml"
scalar() { sed -n -E "s/^$1:[[:space:]]*\"([^\"]*)\".*/\1/p" "$YAML" | head -n 1; }
folded() {
  awk -v key="$1" '
    $0 ~ "^"key":[[:space:]]*>" { grab=1; next }
    grab && /^[[:space:]]+/ { sub(/^[[:space:]]+/, ""); out = out (out == "" ? "" : " ") $0; next }
    grab { exit }
    END { print out }' "$YAML"
}
json_escape() {
  local s="$1"
  s="${s//\\/\\\\}"; s="${s//\"/\\\"}"; s="${s//$'\n'/\\n}"; s="${s//$'\r'/\\r}"; s="${s//$'\t'/\\t}"
  printf '"%s"' "$s"
}
md5_of() { if command -v md5sum >/dev/null 2>&1; then md5sum "$1" | cut -d' ' -f1; else md5 -q "$1"; fi; }
sha256_of() { if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi; }

[ -n "$VERSION" ] || VERSION="$(scalar version)"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "Version must have 4 numbers, got '$VERSION'" >&2; exit 1; }

GUID="$(scalar guid)"; NAME="$(scalar name)"; OWNER="$(scalar owner)"; CATEGORY="$(scalar category)"
OVERVIEW="$(scalar overview)"; DESCRIPTION="$(folded description)"

SEMVER="$(echo "$VERSION" | cut -d. -f1-3)"
CHANGELOG=""
if [ -f "$ROOT/CHANGELOG.md" ]; then
  CHANGELOG="$(awk -v v="$SEMVER" '
    index($0, "## [" v "]") == 1 { grab=1; next }
    grab && /^## \[/ { exit }
    grab { print }' "$ROOT/CHANGELOG.md" | sed -e '/./,$!d' | sed -e :a -e '/^\n*$/{$d;N;ba' -e '}')"
fi
[ -n "$CHANGELOG" ] || CHANGELOG="$(folded changelog)"

if [ -n "${SOURCE_DATE_EPOCH:-}" ]; then EPOCH="$SOURCE_DATE_EPOCH"
elif EPOCH="$(git -C "$ROOT" log -1 --format=%ct 2>/dev/null)" && [ -n "$EPOCH" ]; then :
else EPOCH=1767225600; fi
TIMESTAMP="$(date -u -d "@$EPOCH" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -r "$EPOCH" +%Y-%m-%dT%H:%M:%SZ)"
TOUCH_STAMP="$(TZ=UTC date -u -d "@$EPOCH" +%Y%m%d%H%M.%S 2>/dev/null || TZ=UTC date -u -r "$EPOCH" +%Y%m%d%H%M.%S)"

# builds: id framework targetAbi (one per line)
BUILDS="$(awk '
  /^[[:space:]]*-[[:space:]]*id:/ { gsub(/.*id:[[:space:]]*"|".*/, ""); id=$0; next }
  id != "" && /framework:/ { gsub(/.*framework:[[:space:]]*"|".*/, ""); fw=$0; next }
  id != "" && /targetAbi:/ { gsub(/.*targetAbi:[[:space:]]*"|".*/, ""); print id, fw, $0; id="" }' "$YAML")"

mkdir -p "$ARTIFACTS"
FOUND=0
while read -r ID FW ABI; do
  [ -n "$ID" ] || continue
  if [ -n "$ONLY" ] && [ "$ONLY" != "$ID" ]; then continue; fi
  FOUND=1
  STAGING="$ARTIFACTS/staging/$ID"
  rm -rf "$STAGING"; mkdir -p "$STAGING"

  echo "==> Publishing $ID ($FW, targetAbi $ABI, version $VERSION)"
  dotnet build "$PROJECT" -c Release -f "$FW" --no-incremental "-p:Version=$VERSION" -p:ContinuousIntegrationBuild=true --nologo -v:minimal
  dotnet publish "$PROJECT" -c Release -f "$FW" --no-build -o "$STAGING" "-p:Version=$VERSION" -p:ContinuousIntegrationBuild=true --nologo -v:minimal

  for f in "$STAGING"/*.dll; do
    [ "$(basename "$f")" = "$DLL" ] || { echo "Unexpected DLL in publish output: $(basename "$f")" >&2; exit 1; }
  done
  [ -f "$STAGING/$DLL" ] || { echo "$DLL not found in publish output" >&2; exit 1; }

  {
    printf '{\n'
    printf '    "category": %s,\n' "$(json_escape "$CATEGORY")"
    printf '    "changelog": %s,\n' "$(json_escape "$CHANGELOG")"
    printf '    "description": %s,\n' "$(json_escape "$DESCRIPTION")"
    printf '    "guid": %s,\n' "$(json_escape "$GUID")"
    printf '    "name": %s,\n' "$(json_escape "$NAME")"
    printf '    "overview": %s,\n' "$(json_escape "$OVERVIEW")"
    printf '    "owner": %s,\n' "$(json_escape "$OWNER")"
    printf '    "targetAbi": %s,\n' "$(json_escape "$ABI")"
    printf '    "timestamp": %s,\n' "$(json_escape "$TIMESTAMP")"
    printf '    "version": %s,\n' "$(json_escape "$VERSION")"
    printf '    "assemblies": [ %s ]\n' "$(json_escape "$DLL")"
    printf '}\n'
  } > "$STAGING/meta.json"

  ZIPNAME="ratings-recommendations_${VERSION}_${ID}.zip"
  ZIP="$ARTIFACTS/$ZIPNAME"
  rm -f "$ZIP"
  TZ=UTC touch -t "$TOUCH_STAMP" "$STAGING/$DLL" "$STAGING/meta.json"
  (cd "$STAGING" && TZ=UTC zip -X -q -9 "$ZIP" "$DLL" meta.json)

  MD5="$(md5_of "$ZIP")"; SHA="$(sha256_of "$ZIP")"
  printf '%s  %s\n' "$MD5" "$ZIPNAME" > "$ZIP.md5"
  printf '%s  %s\n' "$SHA" "$ZIPNAME" > "$ZIP.sha256"
  printf '    %s\n    md5    %s\n    sha256 %s\n' "$ZIP" "$MD5" "$SHA"
done <<< "$BUILDS"

[ "$FOUND" = 1 ] || { echo "No build matched '${ONLY}'" >&2; exit 1; }
