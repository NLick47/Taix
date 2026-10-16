#!/usr/bin/env bash
# 组装 TaixNotify.app：构建 Swift 产物 + Info.plist + Taix 图标 + ad-hoc 签名
#
# 用法：taix-notify-macos/make-app.sh <输出目录>
#   → <输出目录>/TaixNotify.app
#
# 打包脚本（taix-installer/build-dmg.sh）与本机部署都调用它
set -euo pipefail

OUT_DIR="${1:?用法: make-app.sh <输出目录>}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

cd "$ROOT/taix-notify-macos"
swift build -c release 2>&1 | tail -1

APP="$OUT_DIR/TaixNotify.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp .build/release/TaixNotify "$APP/Contents/MacOS/TaixNotify"

# 图标：使用产品图标（taix.icns）。宿主 App 是各插件共用的单图标（macOS 限制），
# 插件图标不进通知（避免双图标）；taix.icns 缺失时才从插件 PNG 应急生成
make_icns() {
    local png="$1" out="$2" work iconset
    work="$(mktemp -d)"
    iconset="$work/AppIcon.iconset"
    mkdir -p "$iconset"
    sips -z 16 16 "$png" --out "$iconset/icon_16x16.png" >/dev/null
    sips -z 32 32 "$png" --out "$iconset/icon_16x16@2x.png" >/dev/null
    sips -z 32 32 "$png" --out "$iconset/icon_32x32.png" >/dev/null
    sips -z 64 64 "$png" --out "$iconset/icon_32x32@2x.png" >/dev/null
    sips -z 128 128 "$png" --out "$iconset/icon_128x128.png" >/dev/null
    sips -z 256 256 "$png" --out "$iconset/icon_128x128@2x.png" >/dev/null
    sips -z 256 256 "$png" --out "$iconset/icon_256x256.png" >/dev/null
    sips -z 512 512 "$png" --out "$iconset/icon_256x256@2x.png" >/dev/null
    iconutil -c icns "$iconset" -o "$out"
    rm -rf "$work"
}

if [ -f "$ROOT/taix-shell/resources/icons/taix.icns" ]; then
    cp "$ROOT/taix-shell/resources/icons/taix.icns" "$APP/Contents/Resources/AppIcon.icns"
    echo "icon: taix.icns（产品图标）"
elif [ -f "$ROOT/taix-plugins/reminder/assets/icon.png" ]; then
    make_icns "$ROOT/taix-plugins/reminder/assets/icon.png" "$APP/Contents/Resources/AppIcon.icns"
    echo "icon: reminder icon.png（应急生成）"
else
    echo "WARN: 未找到任何图标资源" >&2
fi

cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>zh_CN</string>
    <key>CFBundleExecutable</key>
    <string>TaixNotify</string>
    <key>CFBundleIdentifier</key>
    <string>com.taix.notify</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>Taix</string>
    <key>CFBundleDisplayName</key>
    <string>Taix</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>LSUIElement</key>
    <true/>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

codesign --force --deep --sign - "$APP" >/dev/null 2>&1 || codesign --force --sign - "$APP"
echo "assembled: $APP"
