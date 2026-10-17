#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:-0.0.0}"
OUT_DIR="${2:-$ROOT/dist}"
TARGET="${TAIX_TARGET:-aarch64-apple-darwin}"

say() { printf "\n== %s ==\n" "$1"; }

die() { echo "ERROR: $*" >&2; exit 1; }

command -v npm >/dev/null 2>&1 || die "npm 不在 PATH 里（需要 Node 20+）"
command -v cargo >/dev/null 2>&1 || die "cargo 不在 PATH 里"
command -v dotnet >/dev/null 2>&1 || die "dotnet 不在 PATH 里（需要 .NET 10 SDK）"
command -v swift >/dev/null 2>&1 || die "swift 不在 PATH 里（需要 Xcode 命令行工具）"

build_frontend() {
    local dir="$1" name="$2"
    say "前端：$name"
    [ -f "$dir/package.json" ] || die "$dir/package.json 不存在"
    if [ "${TAIX_SKIP_FRONTEND:-0}" != "1" ]; then
        if [ ! -d "$dir/node_modules" ]; then
            npm ci --prefix "$dir"
        fi
        npm run build --prefix "$dir"
    fi
}

build_frontend "$ROOT/taix-webui" "webui（server 托管 /ui/*）"
[ -f "$ROOT/taix-webui/dist/index.html" ] || die "taix-webui/dist/index.html 缺失，前端构建失败"

build_frontend "$ROOT/taix-plugins/reminder/ui" "reminder 插件配置页"
[ -f "$ROOT/taix-plugins/reminder/ui/dist/config.js" ] || die "reminder/ui/dist/config.js 缺失，前端构建失败"

say "Rust：target $TARGET"
rustup target add "$TARGET" >/dev/null 2>&1 || true
for crate in taix-server taix-shell taix-plugins/reminder; do
    echo "-- $crate"
    (cd "$ROOT/$crate" && cargo build --release --target "$TARGET")
done

say "Swift：taix-monitor-macos"
if ! (cd "$ROOT/taix-monitor-macos" && swift build -c release --arch arm64); then
    echo "SwiftPM 沙箱不可用，改用 --disable-sandbox 重试"
    (cd "$ROOT/taix-monitor-macos" && swift build -c release --arch arm64 --disable-sandbox)
fi
MONITOR_BIN="$ROOT/taix-monitor-macos/.build/release/taix-monitor"
[ -f "$MONITOR_BIN" ] || die "Swift 产物缺失：$MONITOR_BIN"
strip "$MONITOR_BIN" 2>/dev/null || true

say "打包 .app + TaixTools + DMG（版本 ${VERSION}）"
mkdir -p "$OUT_DIR"
bash "$ROOT/taix-installer/build-dmg.sh" "$VERSION" "$OUT_DIR" "$TARGET"

DMG="$OUT_DIR/Taix-$VERSION-macos-arm64.dmg"
[ -f "$DMG" ] || die "DMG 未生成：$DMG"

say "产物"
ls -lh "$DMG"
echo "校验关键内容"
echo "  DMG 内应包含：Taix.app / TaixTools/{taix-shell,taix-server,taix-monitor-macos,webui,plugins/reminder,TaixNotify.app}"
