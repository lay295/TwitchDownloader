#!/usr/bin/env bash
# Codesign + notarize a TwitchDownloaderAvalonia .app bundle.
# Follows: https://docs.avaloniaui.net/docs/deployment/macos#packaging-in-github-actions-workflow
#
# Expected GitHub Actions secrets (maintainer-owned Apple Developer ID):
#   MACOS_CERTIFICATE              base64-encoded Developer ID Application .p12
#   MACOS_CERTIFICATE_PWD          password for that .p12
#   APPLE_ID                       Apple ID email used for notarization
#   APPLE_TEAM_ID                  10-character Team ID
#   APPLE_APP_SPECIFIC_PASSWORD    app-specific password for notarytool
# Optional:
#   MACOS_SIGNING_IDENTITY         e.g. "Developer ID Application: Name (TEAMID)"
#                                  If unset, the first Developer ID Application identity is used.
set -euo pipefail

usage() {
  echo "Usage: $0 --app <TwitchDownloaderAvalonia.app>" >&2
  exit 1
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENTITLEMENTS="$SCRIPT_DIR/TwitchDownloaderAvalonia.entitlements"
APP_PATH=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --app)
      APP_PATH="${2:-}"
      shift 2
      ;;
    *)
      usage
      ;;
  esac
done

[[ -n "$APP_PATH" ]] || usage
[[ -d "$APP_PATH" ]] || { echo "App bundle not found: $APP_PATH" >&2; exit 1; }
[[ -f "$ENTITLEMENTS" ]] || { echo "Entitlements not found: $ENTITLEMENTS" >&2; exit 1; }

: "${MACOS_CERTIFICATE:?MACOS_CERTIFICATE is required}"
: "${MACOS_CERTIFICATE_PWD:?MACOS_CERTIFICATE_PWD is required}"
: "${APPLE_ID:?APPLE_ID is required}"
: "${APPLE_TEAM_ID:?APPLE_TEAM_ID is required}"
: "${APPLE_APP_SPECIFIC_PASSWORD:?APPLE_APP_SPECIFIC_PASSWORD is required}"

KEYCHAIN_PASSWORD="${KEYCHAIN_PASSWORD:-$(openssl rand -base64 32)}"
KEYCHAIN_NAME="${KEYCHAIN_NAME:-twitchdownloader.build.keychain}"
NOTARY_PROFILE="${NOTARY_PROFILE:-TwitchDownloaderNotary}"
CERTIFICATE_PATH="${RUNNER_TEMP:-/tmp}/macos-certificate.p12"
ZIP_PATH="${RUNNER_TEMP:-/tmp}/$(basename "$APP_PATH").notarize.zip"

cleanup() {
  rm -f "$CERTIFICATE_PATH" "$ZIP_PATH"
  security delete-keychain "$KEYCHAIN_NAME" >/dev/null 2>&1 || true
}
trap cleanup EXIT

security create-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN_NAME"
security default-keychain -s "$KEYCHAIN_NAME"
security unlock-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN_NAME"
security set-keychain-settings -t 3600 -u "$KEYCHAIN_NAME"

echo "$MACOS_CERTIFICATE" | base64 --decode > "$CERTIFICATE_PATH"
security import "$CERTIFICATE_PATH" \
  -k "$KEYCHAIN_NAME" \
  -P "$MACOS_CERTIFICATE_PWD" \
  -T /usr/bin/codesign \
  -T /usr/bin/security
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$KEYCHAIN_PASSWORD" "$KEYCHAIN_NAME"

if [[ -z "${MACOS_SIGNING_IDENTITY:-}" ]]; then
  MACOS_SIGNING_IDENTITY="$(
    security find-identity -v -p codesigning "$KEYCHAIN_NAME" \
      | awk -F\" '/Developer ID Application/ { print $2; exit }'
  )"
fi
[[ -n "$MACOS_SIGNING_IDENTITY" ]] || {
  echo "No Developer ID Application identity found. Set MACOS_SIGNING_IDENTITY." >&2
  exit 1
}
echo "Using signing identity: $MACOS_SIGNING_IDENTITY"

xcrun notarytool store-credentials "$NOTARY_PROFILE" \
  --apple-id "$APPLE_ID" \
  --team-id "$APPLE_TEAM_ID" \
  --password "$APPLE_APP_SPECIFIC_PASSWORD"

# Sign nested Mach-O content first, then the bundle (do not use --deep).
while IFS= read -r -d '' fname; do
  if file -b "$fname" | grep -q 'Mach-O'; then
    echo "[INFO] Signing $fname"
    codesign --force --timestamp --options=runtime \
      --entitlements "$ENTITLEMENTS" \
      --sign "$MACOS_SIGNING_IDENTITY" \
      "$fname"
  fi
done < <(find "$APP_PATH/Contents/MacOS" -type f -print0)

echo "[INFO] Signing app bundle $APP_PATH"
codesign --force --timestamp --options=runtime \
  --entitlements "$ENTITLEMENTS" \
  --sign "$MACOS_SIGNING_IDENTITY" \
  "$APP_PATH"

codesign --verify --verbose=2 "$APP_PATH"

ditto -c -k --sequesterRsrc --keepParent "$APP_PATH" "$ZIP_PATH"
xcrun notarytool submit "$ZIP_PATH" --keychain-profile "$NOTARY_PROFILE" --wait
xcrun stapler staple "$APP_PATH"
xcrun stapler validate "$APP_PATH"

echo "Signed and notarized $APP_PATH"
