#!/bin/bash

set -e

VERSION="${1:-1.0.10}"
OUTPUT_DIR="${2:-.}"
TARGET_DIR="${3:-}"
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

    echo "[3.5/5] Building taix-plugin-reminder..."
    cd "$ROOT_DIR/taix-plugins/reminder"
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

# 插件系统产物（docs/plugin-plan/07-packaging.md §1）：webui 由 server 托管 /ui/*
# 缺产物要**让打包失败**：静静打出一个没有插件/界面的包，用户只会看到"插件列表是空的"
if [ ! -f "$ROOT_DIR/taix-webui/dist/index.html" ]; then
    echo "ERROR: taix-webui/dist/index.html 不存在，先运行 npm run build --prefix taix-webui" >&2
    exit 1
fi
cp -r "$ROOT_DIR/taix-webui/dist" "$TOOLS_DIR/webui"
echo "Copied taix-webui dist -> TaixTools/webui"
# 侧车插件安装区（shell 扫描空集合法；macOS 首版无侧车 widget，目录占位）
mkdir -p "$TOOLS_DIR/plugins"
# 侧车插件「使用时长提醒」：构建产物 + 清单一起进包
if [ -n "$TARGET_DIR" ]; then
    PLUGIN_SRC="$ROOT_DIR/taix-plugins/reminder/target/$TARGET_DIR/release/taix-plugin-reminder"
else
    PLUGIN_SRC="$ROOT_DIR/taix-plugins/reminder/target/release/taix-plugin-reminder"
fi
if [ ! -f "$PLUGIN_SRC" ]; then
    echo "ERROR: taix-plugin-reminder 未构建，先 cd taix-plugins/reminder && cargo build --release" >&2
    exit 1
fi
mkdir -p "$TOOLS_DIR/plugins/reminder"
cp "$ROOT_DIR/taix-plugins/reminder/plugin.json" "$TOOLS_DIR/plugins/reminder/"
cp "$PLUGIN_SRC" "$TOOLS_DIR/plugins/reminder/"
chmod +x "$TOOLS_DIR/plugins/reminder/taix-plugin-reminder"
echo "Copied taix-plugin-reminder -> TaixTools/plugins/reminder"

# 插件自带的配置 UI，configUi 指向它；缺产物让打包失败，避免配置页静默回退通用表单
if [ ! -f "$ROOT_DIR/taix-plugins/reminder/ui/dist/config.js" ]; then
    echo "ERROR: taix-plugins/reminder/ui/dist/config.js 不存在，先运行 (cd taix-plugins/reminder/ui && npm ci && npm run build)" >&2
    exit 1
fi
mkdir -p "$TOOLS_DIR/plugins/reminder/ui"
cp "$ROOT_DIR/taix-plugins/reminder/ui/dist/config.js" "$TOOLS_DIR/plugins/reminder/ui/"
echo "Copied taix-plugin-reminder ui -> TaixTools/plugins/reminder/ui"

# macOS 通知宿主：通知以「Taix」名称与图标推送（缺它时 taix-notify 自动回退 osascript）
"$ROOT_DIR/taix-notify-macos/make-app.sh" "$TOOLS_DIR"
echo "Assembled TaixNotify.app -> TaixTools/TaixNotify.app"

# TaixTools 下的可执行文件不在 .app 的 `codesign --deep` 覆盖范围内，逐个 ad-hoc 签名
# （否则用户机器上可能因隔离属性直接拒绝执行——server/monitor/插件三类都要覆盖，
#   所以这一步必须在插件拷进 TaixTools 之后）
for bin in "$TOOLS_DIR/taix-shell" "$TOOLS_DIR/taix-server" "$TOOLS_DIR/taix-monitor-macos" "$TOOLS_DIR"/plugins/*/taix-plugin-*; do
    if [ -f "$bin" ]; then
        if codesign --force --sign - "$bin" >/dev/null 2>&1; then
            echo "Signed (ad-hoc): $(basename "$bin")"
        else
            echo "WARN: codesign 失败: $bin"
        fi
    fi
done

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

# launchd 里有旧登记时 bootstrap 会报 5 I/O error，先清一次再装
launchctl bootout "gui/$(id -u)/com.taix.shell" 2>/dev/null || true

# 顺手清掉旧版本留下的输出重定向文件
rm -f "$LOG_DIR/shell.log"

# 老 load -w 的 -w 会清 Disabled 覆盖，bootstrap 不会
launchctl enable "gui/$(id -u)/com.taix.shell" 2>/dev/null || true

if ! launchctl bootstrap "gui/$(id -u)" "$PLIST_PATH" 2>/dev/null; then
    launchctl load -w "$PLIST_PATH" 2>/dev/null || true
fi

echo "LaunchAgent installed at $SCRIPT_DIR"
SCRIPT
chmod +x "$TOOLS_DIR/install-launchagent.sh"

cat > "$TOOLS_DIR/uninstall-launchagent.sh" << 'SCRIPT'
#!/bin/bash
launchctl bootout "gui/$(id -u)/com.taix.shell" 2>/dev/null || true
rm -f "$HOME/Library/LaunchAgents/com.taix.shell.plist"
echo "LaunchAgent removed"
SCRIPT
chmod +x "$TOOLS_DIR/uninstall-launchagent.sh"

cat > "$TOOLS_DIR/uninstall.sh" << 'SCRIPT'
#!/bin/bash

echo "Uninstalling Taix..."

launchctl bootout "gui/$(id -u)/com.taix.shell" 2>/dev/null || true
rm -f "$HOME/Library/LaunchAgents/com.taix.shell.plist"

pkill -x "taix-shell" 2>/dev/null || true
pkill -x "taix-server" 2>/dev/null || true
pkill -x "taix-monitor-macos" 2>/dev/null || true
pkill -x "Taix" 2>/dev/null || true
# 插件绝不自杀（SDK park 常驻），必须在这里杀掉，否则卸载后仍是常驻孤儿；
# 名字超 16 字符，pkill -x 的 comm 匹配不可靠，按完整命令行匹配安装目录
pkill -f "/TaixTools/plugins/" 2>/dev/null || true

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
