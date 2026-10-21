#!/bin/zsh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="$ROOT/native/DSHNotchPet.app"
BUILD="$ROOT/native/.build"
SOURCES=("$ROOT/native/Sources/AppDelegate.swift" "$ROOT/native/Sources/AppLog.swift" "$ROOT/native/Sources/DSHLauncher.swift" "$ROOT/native/Sources/L10n.swift" "$ROOT/native/Sources/Models.swift" "$ROOT/native/Sources/NotchPanel.swift" "$ROOT/native/Sources/PetView.swift" "$ROOT/native/Sources/SocketClient.swift" "$ROOT/native/Sources/main.swift")

rm -rf "$APP" "$BUILD"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$BUILD"

cp "$ROOT/native/Info.plist" "$APP/Contents/Info.plist"

xcrun swiftc \
  -swift-version 5 \
  -O \
  -target arm64-apple-macos14.0 \
  -framework AppKit -framework SwiftUI -framework Combine \
  "${SOURCES[@]}" \
  -o "$BUILD/DSHNotchPet-arm64"

xcrun swiftc \
  -swift-version 5 \
  -O \
  -target x86_64-apple-macos14.0 \
  -framework AppKit -framework SwiftUI -framework Combine \
  "${SOURCES[@]}" \
  -o "$BUILD/DSHNotchPet-x86_64"

lipo -create "$BUILD/DSHNotchPet-arm64" "$BUILD/DSHNotchPet-x86_64" -output "$APP/Contents/MacOS/DSHNotchPet"
chmod +x "$APP/Contents/MacOS/DSHNotchPet"
codesign --force --sign - --timestamp=none "$APP"
codesign --verify --strict "$APP"

echo "Built $APP"
file "$APP/Contents/MacOS/DSHNotchPet"
