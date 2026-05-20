#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [string]$OutputDir = "./releases"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $RepoRoot "publish"

# 1. Publish .NET Client
Write-Host "[+] Publishing Taix.Client (self-contained)..." -ForegroundColor Green
$csproj = Join-Path $RepoRoot "Taix.Client" "Taix.Client.csproj"
dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed"
}

# 2. Copy Rust executables
$rustExes = @(
    @{ Source = Join-Path $RepoRoot "taix-shell" "target" "release" "taix-shell.exe";     Name = "taix-shell.exe" }
    @{ Source = Join-Path $RepoRoot "taix-server" "target" "release" "taix-server.exe";   Name = "taix-server.exe" }
    @{ Source = Join-Path $RepoRoot "taix-monitor-windows" "target" "release" "taix-monitor-windows.exe"; Name = "taix-monitor-windows.exe" }
)

foreach ($exe in $rustExes) {
    if (Test-Path $exe.Source) {
        Copy-Item $exe.Source -Destination (Join-Path $PublishDir $exe.Name) -Force
        Write-Host "[+] Copied $($exe.Name)"
    } else {
        Write-Warning "[-] $($exe.Name) not found at $($exe.Source). Please build Rust projects first with: cargo build --release"
    }
}

# 3. Ensure vpk is installed
$vpk = Get-Command "vpk" -ErrorAction SilentlyContinue
if (-not $vpk) {
    Write-Host "[+] Installing vpk (Velopack CLI)..." -ForegroundColor Cyan
    dotnet tool install -g vpk
}

# 4. Pack with Velopack
Write-Host "[+] Packing with Velopack v$Version..." -ForegroundColor Green
$mainExe = "Taix.exe"
$packId = "nlick47.taix"

& vpk pack `
    --packId $packId `
    --packVersion $Version `
    --packDir $PublishDir `
    --mainExe $mainExe `
    --outputDir $OutputDir

if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed"
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Velopack build complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output: $(Resolve-Path $OutputDir)"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Test locally by running $OutputDir\Setup.exe"
Write-Host "  2. Upload to GitHub Releases:"
Write-Host "     vpk upload github --repoUrl https://github.com/nlick47/taix --publish --tag v$Version"
