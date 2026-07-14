#!/bin/bash
# Taix 安装器打包脚本
# 流程：
# 1. LZMA2 打包所有文件成 payload
# 2. 编译安装器 (不带 payload)
# 3. 追加 payload 到安装器末尾

set -e

SOURCE_DIR="$1"
VERSION="$2"
OUTPUT_DIR="${3:-.}"

# 获取脚本所在目录作为 INSTALLER_DIR
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
INSTALLER_DIR="$SCRIPT_DIR"
PACK_PAYLOAD_PATH="$INSTALLER_DIR/target/release/pack-payload.exe"

TEMP_DIR="/tmp/taix-build-$VERSION"

echo "============================================"
echo "  Taix Installer Build Script"
echo "============================================"
echo ""
echo "SOURCE_DIR: $SOURCE_DIR"
echo "VERSION:    $VERSION"
echo "OUTPUT_DIR: $OUTPUT_DIR"
echo ""

# 检查依赖
if [ ! -d "$SOURCE_DIR" ]; then
    echo "ERROR: Source directory not found: $SOURCE_DIR"
    exit 1
fi

# 清理并创建临时目录
rm -rf "$TEMP_DIR"
mkdir -p "$TEMP_DIR"

echo "[1/3] 复制文件..."
cp -r "$SOURCE_DIR"/* "$TEMP_DIR/"

echo "[2/3] 编译安装器并打包 payload..."
cargo build --release --manifest-path "$INSTALLER_DIR/Cargo.toml"

# 检查编译结果
if [ ! -f "$PACK_PAYLOAD_PATH" ]; then
    echo "ERROR: pack-payload.exe not found after build: $PACK_PAYLOAD_PATH"
    exit 1
fi
if [ ! -f "$INSTALLER_DIR/target/release/taix-installer.exe" ]; then
    echo "ERROR: taix-installer.exe not found after build"
    exit 1
fi

# UPX 压缩安装器本体
echo "[2.5/3] UPX 压缩安装器..."
if command -v upx &> /dev/null; then
    upx --best --compress-resources=0 "$INSTALLER_DIR/target/release/taix-installer.exe"
    UPX_SIZE=$(ls -lh "$INSTALLER_DIR/target/release/taix-installer.exe" | awk '{print $5}')
    echo "UPX 压缩完成，大小: $UPX_SIZE"
else
    echo "WARNING: 未找到 upx，跳过压缩"
fi

PAYLOAD_FILE="$TEMP_DIR/payload.bin"
"$PACK_PAYLOAD_PATH" "$TEMP_DIR" "$PAYLOAD_FILE"

echo "[3/3] 合并 payload 到安装器..."
FINAL_INSTALLER="$OUTPUT_DIR/Taix-$VERSION-Setup.exe"

# 复制安装器
cp "$INSTALLER_DIR/target/release/taix-installer.exe" "$FINAL_INSTALLER"

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