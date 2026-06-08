# Taix 安装器打包脚本
# 流程：UPX压缩Rust exe -> LZMA2打包payload -> 编译安装器

param(
    [string]$SourceDir,
    [string]$OutputDir = "Z:\Github\Taix",
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"

$InstallerDir = "Z:\Github\Taix\taix-installer"
$UpxPath = "$InstallerDir\tools\upx.exe"
$PackPayloadPath = "$InstallerDir\target\release\pack-payload.exe"

# 检查依赖
if (-not (Test-Path $UpxPath)) {
    Write-Error "UPX not found: $UpxPath"
}
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

Write-Host "[1/4] 复制文件..." -ForegroundColor Yellow

# Rust exe 列表（需要 UPX 压缩）
$RustExes = @("taix-server.exe", "taix-monitor-windows.exe", "taix-shell.exe")

# 创建 Rust exe 临时目录
$RustExeDir = Join-Path $TempDir "rust-exe"
New-Item -ItemType Directory -Path $RustExeDir | Out-Null

# 复制源目录所有内容
Copy-Item -Path "$SourceDir\*" -Destination $TempDir -Recurse -Force

# 复制 Rust exe 到单独目录用于 UPX
foreach ($exeName in $RustExes) {
    $srcExe = Join-Path $TempDir $exeName
    if (Test-Path $srcExe) {
        Copy-Item $srcExe $RustExeDir -Force
    }
}

Write-Host "[2/4] UPX 压缩 Rust exe..." -ForegroundColor Yellow

# UPX 压缩 Rust exe
$RustExeFiles = Get-ChildItem -Path $RustExeDir -Filter "*.exe"
foreach ($exe in $RustExeFiles) {
    Write-Host "  UPX: $($exe.Name)"
    & $UpxPath --best --ultra-brute $exe.FullName | Out-Null

    # 复制压缩后的 exe 回主目录
    $targetExe = Join-Path $TempDir $exe.Name
    Copy-Item $exe.FullName $targetExe -Force
}

Write-Host "[3/4] LZMA2 打包 payload..." -ForegroundColor Yellow

$PayloadFile = Join-Path $env:TEMP "payload-$Version.lzma"
& $PackPayloadPath $TempDir $PayloadFile

Write-Host "[4/4] 编译安装器..." -ForegroundColor Yellow

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