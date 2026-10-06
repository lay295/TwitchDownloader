#!/usr/bin/env bash
# Creates a macOS .app bundle from a TwitchDownloaderAvalonia publish output.
# See: https://docs.avaloniaui.net/docs/deployment/macos
set -euo pipefail

usage() {
  echo "Usage: $0 --publish-dir <path> --output <TwitchDownloaderAvalonia.app> [--version <x.y.z>]" >&2
  exit 1
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
INFO_PLIST_SRC="$SCRIPT_DIR/Info.plist"
ICON_SRC="$PROJECT_DIR/Assets/icon.icns"
PUBLISH_DIR=""
OUTPUT_APP=""
VERSION=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --publish-dir)
      PUBLISH_DIR="${2:-}"
      shift 2
      ;;
    --output)
      OUTPUT_APP="${2:-}"
      shift 2
      ;;
    --version)
      VERSION="${2:-}"
      shift 2
      ;;
    *)
      usage
      ;;
  esac
done

[[ -n "$PUBLISH_DIR" && -n "$OUTPUT_APP" ]] || usage
[[ -d "$PUBLISH_DIR" ]] || { echo "Publish directory not found: $PUBLISH_DIR" >&2; exit 1; }
[[ -f "$INFO_PLIST_SRC" ]] || { echo "Info.plist not found: $INFO_PLIST_SRC" >&2; exit 1; }
[[ -f "$ICON_SRC" ]] || { echo "Icon not found: $ICON_SRC" >&2; exit 1; }

EXECUTABLE="$PUBLISH_DIR/TwitchDownloaderAvalonia"
[[ -f "$EXECUTABLE" ]] || { echo "Executable not found: $EXECUTABLE" >&2; exit 1; }

rm -rf "$OUTPUT_APP"
mkdir -p "$OUTPUT_APP/Contents/MacOS" "$OUTPUT_APP/Contents/Resources"

cp "$INFO_PLIST_SRC" "$OUTPUT_APP/Contents/Info.plist"
if [[ -n "$VERSION" ]]; then
  /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $VERSION" "$OUTPUT_APP/Contents/Info.plist"
  /usr/libexec/PlistBuddy -c "Set :CFBundleVersion $VERSION" "$OUTPUT_APP/Contents/Info.plist"
fi

cp "$ICON_SRC" "$OUTPUT_APP/Contents/Resources/icon.icns"
cp -a "$PUBLISH_DIR"/. "$OUTPUT_APP/Contents/MacOS/"
chmod +x "$OUTPUT_APP/Contents/MacOS/TwitchDownloaderAvalonia"
if [[ -f "$OUTPUT_APP/Contents/MacOS/TwitchDownloaderAvalonia.Updater" ]]; then
  chmod +x "$OUTPUT_APP/Contents/MacOS/TwitchDownloaderAvalonia.Updater"
fi

echo "Created $OUTPUT_APP"
