#!/usr/bin/env bash
# build-macos.sh — builds ResellerSystem-macOS.dmg
#
# MUST run on an actual Mac: .app bundling, ad-hoc/codesign, and hdiutil
# (all built into macOS, free) have no Windows equivalent, which is why
# this is a separate script from build-release.ps1 rather than something
# that script could ever invoke.
#
# Produces an UNSIGNED, ad-hoc-signed .app + .dmg. Ad-hoc signing lets the
# app run on the machine that built it and satisfies Gatekeeper's basic
# integrity check; distributing to other Macs without a paid Apple
# Developer ID will show a "cannot verify developer" warning on first
# launch (user right-clicks -> Open to bypass) until real notarization is
# set up later — that is a $99/year *optional* step, not required for
# Stage 1's free/open-source requirement.
set -euo pipefail

VERSION="${1:-0.1.0}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST_DIR="$ROOT_DIR/dist/client-macos"
ARTIFACTS_DIR="$ROOT_DIR/artifacts"
APP_NAME="Reseller System"
BUNDLE_DIR="$DIST_DIR/$APP_NAME.app"

echo "=== 1/5 Cleaning ==="
rm -rf "$DIST_DIR"
mkdir -p "$DIST_DIR" "$ARTIFACTS_DIR"

echo "=== 2/5 Publishing Desktop.App for macOS (self-contained, osx-arm64) ==="
# Publishing osx-x64 too gives Intel Mac support; build both and lipo them
# into a universal binary if/when needed. Stage 1 ships arm64 (current Macs).
dotnet publish "$ROOT_DIR/src/Desktop.App/Desktop.App.csproj" \
    -c Release -r osx-arm64 --self-contained true \
    -p:Version="$VERSION" \
    -o "$DIST_DIR/publish"

echo "=== 3/5 Assembling .app bundle ==="
mkdir -p "$BUNDLE_DIR/Contents/MacOS" "$BUNDLE_DIR/Contents/Resources"

cp -R "$DIST_DIR/publish/"* "$BUNDLE_DIR/Contents/MacOS/"
chmod +x "$BUNDLE_DIR/Contents/MacOS/Desktop.App"

cat > "$BUNDLE_DIR/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key><string>${APP_NAME}</string>
    <key>CFBundleIdentifier</key><string>com.resellersystem.desktop</string>
    <key>CFBundleVersion</key><string>${VERSION}</string>
    <key>CFBundleShortVersionString</key><string>${VERSION}</string>
    <key>CFBundleExecutable</key><string>Desktop.App</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>LSMinimumSystemVersion</key><string>12.0</string>
    <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

echo "=== 4/5 Ad-hoc signing ==="
codesign --force --deep --sign - "$BUNDLE_DIR"

echo "=== 5/5 Creating .dmg (hdiutil, built into macOS) ==="
DMG_PATH="$ARTIFACTS_DIR/ResellerSystem-macOS.dmg"
rm -f "$DMG_PATH"
hdiutil create -volname "$APP_NAME" -srcfolder "$BUNDLE_DIR" -ov -format UDZO "$DMG_PATH"

echo ""
echo "Done: $DMG_PATH"
echo "Note: this is ad-hoc signed, not notarized. First launch on another"
echo "Mac requires right-click -> Open once. See script header for details."
