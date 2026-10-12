#!/bin/bash
# Builds Voice Changer Pro for Apple Silicon Macs and packages it as a .dmg.
#
#   ./build-mac.sh            # app bundle + DMG in ./dist-mac
#   DOTNET=/path/to/dotnet ./build-mac.sh
#
# Needs the .NET 10 SDK and the Xcode command line tools (codesign, iconutil, hdiutil).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
DOTNET="${DOTNET:-dotnet}"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT/Directory.Build.props")"
OUT="$ROOT/dist-mac"
PUBLISH="$OUT/publish"
APP="$OUT/Voice Changer Pro.app"
DMG="$OUT/VoiceChangerPro-$VERSION-macOS-AppleSilicon.dmg"

echo "==> Voice Changer Pro $VERSION for macOS (arm64)"
rm -rf "$OUT"
mkdir -p "$OUT"

echo "==> Publish"
"$DOTNET" publish "$ROOT/src/VoiceChanger.Mac/VoiceChanger.Mac.csproj" -c Release -r osx-arm64 --self-contained true \
  -p:UseAppHost=true -p:DebugType=none -o "$PUBLISH" -nologo

echo "==> Self-test"
"$PUBLISH/VoiceChangerPro" --selftest

echo "==> App bundle"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH/." "$APP/Contents/MacOS/"
sed "s/__VERSION__/$VERSION/g" "$ROOT/packaging/macos/Info.plist" > "$APP/Contents/Info.plist"
printf 'APPL????' > "$APP/Contents/PkgInfo"

echo "==> Icon"
ICONSET="$OUT/AppIcon.iconset"
mkdir -p "$ICONSET"
"$PUBLISH/VoiceChangerPro" --render-icon "$OUT/icon-1024.png" 1024 >/dev/null
for s in 16 32 128 256 512; do
  sips -z $s $s "$OUT/icon-1024.png" --out "$ICONSET/icon_${s}x${s}.png" >/dev/null
  d=$((s * 2))
  sips -z $d $d "$OUT/icon-1024.png" --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"
rm -rf "$ICONSET"

echo "==> Sign (ad-hoc)"
codesign --force --deep --sign - --timestamp=none "$APP"
codesign --verify --deep --strict "$APP"

echo "==> Disk image"
STAGE="$OUT/dmg"
mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
hdiutil create -volname "Voice Changer Pro" -srcfolder "$STAGE" -ov -format UDZO "$DMG" >/dev/null
rm -rf "$STAGE" "$PUBLISH"

echo "==> Done"
ls -lh "$DMG"
shasum -a 256 "$DMG"
