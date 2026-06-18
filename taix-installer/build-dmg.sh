#!/bin/bash

set -e

VERSION="${1:-1.0.10}"
OUTPUT_DIR="${2:-.}"
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"

# 确保 OUTPUT_DIR 是绝对路径
if [[ "$OUTPUT_DIR" != /* ]]; then
    OUTPUT_DIR="$ROOT_DIR/$OUTPUT_DIR"
fi

APP_NAME="Taix"
TEMP_DIR="/tmp/taix-dmg-build-$VERSION"

# 根据目标架构生成 DMG 名称
if [ -n "$TARGET_DIR" ]; then
    # 从 target 目录名提取架构 (如 aarch64-apple-darwin -> arm64)
    ARCH=$(echo "$TARGET_DIR" | sed 's/-apple-darwin//' | sed 's/aarch64/arm64/' | sed 's/x86_64/x64/')
    DMG_NAME="Taix-$VERSION-macos-$ARCH.dmg"
else
    DMG_NAME="Taix-$VERSION-macos.dmg"
fi

TARGET_DIR="${3:-}"

echo "ROOT_DIR:   $ROOT_DIR"
echo "VERSION:    $VERSION"
echo "OUTPUT_DIR: $OUTPUT_DIR"
echo "TARGET_DIR: ${TARGET_DIR:-<auto>}"
echo ""

rm -rf "$TEMP_DIR"
mkdir -p "$TEMP_DIR"

if [ -z "$TARGET_DIR" ]; then
    echo "[1/5] Building taix-shell..."
    cd "$ROOT_DIR/taix-shell"
    cargo build --release 2>&1 | tail -3

    echo "[2/5] Building taix-monitor-macos..."
    cd "$ROOT_DIR/taix-monitor-macos"
    swift build -c release 2>&1 | tail -3

    echo "[3/5] Building taix-server..."
    cd "$ROOT_DIR/taix-server"
    cargo build --release 2>&1 | tail -3
fi

echo "[4/5] Publishing Taix.Client (AOT)..."
cd "$ROOT_DIR/Taix.Client"
dotnet publish -c Release -r osx-arm64 --self-contained true \
  -p:PublishAot=true -p:StripSymbols=true \
  -p:AssemblyTitle="$APP_NAME" \
  -p:Product="$APP_NAME" \
  -o "$TEMP_DIR/client-publish" 2>&1 | tail -5

echo "[5/5] Creating .app bundle..."

APP_BUNDLE="$TEMP_DIR/$APP_NAME.app"
CONTENTS_DIR="$APP_BUNDLE/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"

mkdir -p "$MACOS_DIR"
mkdir -p "$RESOURCES_DIR"

cp "$TEMP_DIR/client-publish/Taix" "$MACOS_DIR/Taix"
chmod +x "$MACOS_DIR/Taix"

if ls "$TEMP_DIR/client-publish"/*.dylib 1> /dev/null 2>&1; then
    cp "$TEMP_DIR/client-publish"/*.dylib "$MACOS_DIR/"
fi

cat > "$CONTENTS_DIR/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>zh_CN</string>
    <key>CFBundleExecutable</key>
    <string>Taix</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleIdentifier</key>
    <string>com.taix.client</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

if [ -f "$ROOT_DIR/taix-shell/resources/icons/taix.icns" ]; then
    cp "$ROOT_DIR/taix-shell/resources/icons/taix.icns" "$RESOURCES_DIR/AppIcon.icns"
fi

echo "Signing app..."
codesign --force --deep --sign - "$APP_BUNDLE"

TOOLS_DIR="$TEMP_DIR/TaixTools"
mkdir -p "$TOOLS_DIR"
mkdir -p "$TOOLS_DIR/resources/icons"

if [ -n "$TARGET_DIR" ]; then
    cp "$ROOT_DIR/taix-shell/target/$TARGET_DIR/release/taix-shell" "$TOOLS_DIR/"
else
    cp "$ROOT_DIR/taix-shell/target/release/taix-shell" "$TOOLS_DIR/"
fi
chmod +x "$TOOLS_DIR/taix-shell"

# Swift build output is always in .build/release regardless of --arch
cp "$ROOT_DIR/taix-monitor-macos/.build/release/taix-monitor" "$TOOLS_DIR/taix-monitor-macos"
chmod +x "$TOOLS_DIR/taix-monitor-macos"

if [ -n "$TARGET_DIR" ]; then
    cp "$ROOT_DIR/taix-server/target/$TARGET_DIR/release/taix-server" "$TOOLS_DIR/"
else
    cp "$ROOT_DIR/taix-server/target/release/taix-server" "$TOOLS_DIR/"
fi
chmod +x "$TOOLS_DIR/taix-server"

if [ -f "$ROOT_DIR/taix-shell/resources/icons/taix.icns" ]; then
    cp "$ROOT_DIR/taix-shell/resources/icons/taix.icns" "$TOOLS_DIR/resources/icons/"
fi

cat > "$TOOLS_DIR/install-launchagent.sh" << 'SCRIPT'
#!/bin/bash
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PLIST_PATH="$HOME/Library/LaunchAgents/com.taix.shell.plist"
LOG_DIR="$HOME/Library/Logs/Taix"

mkdir -p "$HOME/Library/LaunchAgents"
mkdir -p "$LOG_DIR"

cat > "$PLIST_PATH" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.taix.shell</string>
    <key>ProgramArguments</key>
    <array>
        <string>$SCRIPT_DIR/taix-shell</string>
        <string>run</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <dict>
        <key>SuccessfulExit</key>
        <false/>
    </dict>
    <key>StandardOutPath</key>
    <string>$LOG_DIR/shell.log</string>
    <key>StandardErrorPath</key>
    <string>$LOG_DIR/shell.err</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>TAIX_EXE_DIR</key>
        <string>$SCRIPT_DIR</string>
    </dict>
</dict>
</plist>
EOF

launchctl load "$PLIST_PATH" 2>/dev/null || true
echo "LaunchAgent installed at $SCRIPT_DIR"
SCRIPT
chmod +x "$TOOLS_DIR/install-launchagent.sh"

cat > "$TOOLS_DIR/uninstall-launchagent.sh" << 'SCRIPT'
#!/bin/bash
launchctl unload "$HOME/Library/LaunchAgents/com.taix.shell.plist" 2>/dev/null || true
rm -f "$HOME/Library/LaunchAgents/com.taix.shell.plist"
echo "LaunchAgent removed"
SCRIPT
chmod +x "$TOOLS_DIR/uninstall-launchagent.sh"

cat > "$TOOLS_DIR/uninstall.sh" << 'SCRIPT'
#!/bin/bash

echo "Uninstalling Taix..."

launchctl unload "$HOME/Library/LaunchAgents/com.taix.shell.plist" 2>/dev/null || true
rm -f "$HOME/Library/LaunchAgents/com.taix.shell.plist"

pkill -x "taix-shell" 2>/dev/null || true
pkill -x "taix-server" 2>/dev/null || true
pkill -x "taix-monitor-macos" 2>/dev/null || true
pkill -x "Taix" 2>/dev/null || true

sleep 1

rm -rf "/Applications/Taix.app"
rm -rf "/Applications/TaixTools"

rm -rf "$HOME/Library/Caches/Taix"
rm -rf "$HOME/Library/Application Support/Taix"
rm -rf "$HOME/Library/Logs/Taix"

rm -f /tmp/taix_daemon.sock
rm -f /tmp/taix-client.sock

echo "Taix has been uninstalled."
SCRIPT
chmod +x "$TOOLS_DIR/uninstall.sh"

echo "[6/6] Creating DMG..."

DMG_TEMP="$TEMP_DIR/dmg-temp"
mkdir -p "$DMG_TEMP"

AOT_SIZE=$(ls -lh "$MACOS_DIR/Taix" | awk '{print $5}')

mv "$APP_BUNDLE" "$DMG_TEMP/"
mv "$TOOLS_DIR" "$DMG_TEMP/"
ln -s /Applications "$DMG_TEMP/Applications"

DMG_OUTPUT="$OUTPUT_DIR/$DMG_NAME"
mkdir -p "$OUTPUT_DIR"
hdiutil create -volname "$APP_NAME" -srcfolder "$DMG_TEMP" -ov -format UDZO "$DMG_OUTPUT"

DMG_SIZE=$(ls -lh "$DMG_OUTPUT" | awk '{print $5}')

echo ""
echo "DMG: $DMG_OUTPUT"
echo "Size: $DMG_SIZE (AOT binary: $AOT_SIZE)"
echo ""

rm -rf "$TEMP_DIR"
