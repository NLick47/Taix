# Taix 安装器打包脚本
# 流程：LZMA2打包payload -> 编译安装器

param(
    [string]$SourceDir,
    [string]$OutputDir = "Z:\Github\Taix",
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"

$InstallerDir = "Z:\Github\Taix\taix-installer"
$PackPayloadPath = "$InstallerDir\target\release\pack-payload.exe"

# 检查依赖
if (-not (Test-Path $PackPayloadPath)) {
    Write-Error "pack-payload.exe not found. Run 'cargo build --release' first"
}
if (-not (Test-Path $SourceDir)) {
    Write-Error "Source directory not found: $SourceDir"
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Taix Installer Build Script" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# 创建临时目录
$TempDir = Join-Path $env:TEMP "taix-build-$Version"
if (Test-Path $TempDir) {
    Remove-Item -Recurse -Force $TempDir
}
New-Item -ItemType Directory -Path $TempDir | Out-Null

Write-Host "[1/3] 复制文件..." -ForegroundColor Yellow

# 复制源目录所有内容
Copy-Item -Path "$SourceDir\*" -Destination $TempDir -Recurse -Force

Write-Host "[2/3] LZMA2 打包 payload..." -ForegroundColor Yellow

$PayloadFile = Join-Path $env:TEMP "payload-$Version.lzma"
& $PackPayloadPath $TempDir $PayloadFile

Write-Host "[3/3] 编译安装器..." -ForegroundColor Yellow

$env:PAYLOAD_FILE = $PayloadFile
Push-Location $InstallerDir
cargo build --release 2>&1 | Select-String "Embedded|Finished"
Pop-Location

# 复制最终安装器
$FinalInstaller = Join-Path $OutputDir "Taix-$Version-Setup.exe"
Copy-Item "$InstallerDir\target\release\taix-installer.exe" $FinalInstaller -Force

$InstallerSize = (Get-Item $FinalInstaller).Length / 1MB

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  构建完成!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "安装器: $FinalInstaller"
Write-Host "大小:   $([math]::Round($InstallerSize, 2)) MB"
Write-Host ""

# 清理临时目录
Remove-Item -Recurse -Force $TempDir
Remove-Item -Force $PayloadFile
