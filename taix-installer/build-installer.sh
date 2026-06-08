#!/bin/bash
# Taix 安装器打包脚本
# 流程：
# 1. UPX 压缩 Rust exe (server, monitor, shell)
# 2. LZMA2 打包所有文件成 payload
# 3. 编译安装器 (不带 payload)
# 4. UPX 压缩安装器 exe
# 5. 追加 payload 到安装器末尾

set -e

SOURCE_DIR="$1"
VERSION="$2"
OUTPUT_DIR="${3:-.}"

# 获取脚本所在目录作为 INSTALLER_DIR
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
INSTALLER_DIR="$SCRIPT_DIR"
UPX_PATH="$INSTALLER_DIR/tools/upx.exe"
PACK_PAYLOAD_PATH="$INSTALLER_DIR/target/release/pack-payload.exe"

TEMP_DIR="/tmp/taix-build-$VERSION"

echo "============================================"
echo "  Taix Installer Build Script v2"
echo "============================================"
echo ""
echo "SOURCE_DIR: $SOURCE_DIR"
echo "VERSION:    $VERSION"
echo "OUTPUT_DIR: $OUTPUT_DIR"
echo ""

# 检查依赖
if [ ! -f "$UPX_PATH" ]; then
    echo "ERROR: UPX not found: $UPX_PATH"
    exit 1
fi
if [ ! -d "$SOURCE_DIR" ]; then
    echo "ERROR: Source directory not found: $SOURCE_DIR"
    exit 1
fi

# 清理并创建临时目录
rm -rf "$TEMP_DIR"
mkdir -p "$TEMP_DIR"

echo "[1/5] 复制文件..."
cp -r "$SOURCE_DIR"/* "$TEMP_DIR/"

echo "[2/5] UPX 压缩 Rust exe..."
RUST_EXES=("taix-server.exe" "taix-monitor-windows.exe" "taix-shell.exe")
for exe in "${RUST_EXES[@]}"; do
    exe_path="$TEMP_DIR/$exe"
    if [ -f "$exe_path" ]; then
        echo "  UPX: $exe"
        "$UPX_PATH" --best --ultra-brute "$exe_path" 2>/dev/null || true
    fi
done

echo "[3/5] 编译安装器并打包 payload..."
cargo build --release --manifest-path "$INSTALLER_DIR/Cargo.toml" 2>&1 | grep -E "Compiling taix-installer|Finished" || true

PAYLOAD_FILE="$TEMP_DIR/payload.bin"
"$PACK_PAYLOAD_PATH" "$TEMP_DIR" "$PAYLOAD_FILE"

echo "[4/5] UPX 压缩安装器..."
INSTALLER_EXE="$INSTALLER_DIR/target/release/taix-installer.exe"
"$UPX_PATH" --best --ultra-brute "$INSTALLER_EXE" 2>/dev/null || true
ls -lh "$INSTALLER_EXE" | awk '{print "  安装器 (UPX后): " $5}'

echo "[5/5] 合并 payload 到安装器..."
FINAL_INSTALLER="$OUTPUT_DIR/Taix-$VERSION-Setup.exe"

# 复制 UPX 后的安装器
cp "$INSTALLER_EXE" "$FINAL_INSTALLER"

# 追加 payload
cat "$PAYLOAD_FILE" >> "$FINAL_INSTALLER"

INSTALLER_SIZE=$(ls -lh "$FINAL_INSTALLER" | awk '{print $5}')

echo ""
echo "============================================"
echo "  构建完成!"
echo "============================================"
echo ""
echo "安装器: $FINAL_INSTALLER"
echo "大小:   $INSTALLER_SIZE"
echo ""

# 清理
rm -rf "$TEMP_DIR"