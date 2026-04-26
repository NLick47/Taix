#Requires -Version 5.1
<#
.SYNOPSIS
    Install Taix suite: create shortcuts and add to Windows startup folder.

.DESCRIPTION
    Creates a Start Menu shortcut for the Taix client, and places shortcuts
    for taix-server and taix-monitor-windows into the Windows Startup folder
    for auto-launch on login (zero registry writes).

.PARAMETER DesktopShortcut
    Also create a shortcut on the Desktop.

.PARAMETER StartServer
    Launch taix-server immediately after installation.

.PARAMETER StartMonitor
    Launch taix-monitor-windows immediately after installation.
#>

[CmdletBinding()]
param(
    [switch]$DesktopShortcut,
    [switch]$StartServer,
    [switch]$StartMonitor
)

$ErrorActionPreference = "Stop"

$InstallDir = $PSScriptRoot
if (-not $InstallDir) {
    $InstallDir = (Get-Location).Path
}

$ExeNames = @{
    Client  = "Taix.exe"
    Server  = "taix-server.exe"
    Monitor = "taix-monitor-windows.exe"
}

# Verify required executables exist
$missing = @()
foreach ($key in $ExeNames.Keys) {
    $path = Join-Path $InstallDir $ExeNames[$key]
    if (-not (Test-Path $path)) {
        $missing += $ExeNames[$key]
    }
}
if ($missing.Count -gt 0) {
    Write-Host "Error: the following required files are missing:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "`nPlease make sure you have extracted the full release package." -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

$WshShell     = New-Object -ComObject WScript.Shell
$StartMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$StartupDir   = $WshShell.SpecialFolders("Startup")

function New-AppShortcut {
    param(
        [string]$TargetPath,
        [string]$ShortcutPath,
        [string]$WorkingDirectory = $InstallDir
    )
    $sc = $WshShell.CreateShortcut($ShortcutPath)
    $sc.TargetPath       = $TargetPath
    $sc.WorkingDirectory = $WorkingDirectory
    $sc.Save()
}

# Client shortcut (Start Menu + optional Desktop)
$clientExe         = Join-Path $InstallDir $ExeNames.Client
$startMenuShortcut = Join-Path $StartMenuDir "Taix.lnk"

New-AppShortcut -TargetPath $clientExe -ShortcutPath $startMenuShortcut
Write-Host "[+] Created Start Menu shortcut: $startMenuShortcut"

if ($DesktopShortcut) {
    $desktopShortcut = Join-Path $env:USERPROFILE "Desktop\Taix.lnk"
    New-AppShortcut -TargetPath $clientExe -ShortcutPath $desktopShortcut
    Write-Host "[+] Created Desktop shortcut: $desktopShortcut"
}

# Add to Startup folder (auto-launch, zero registry)
$serverExe   = Join-Path $InstallDir $ExeNames.Server
$monitorExe  = Join-Path $InstallDir $ExeNames.Monitor

$serverStartupSc  = Join-Path $StartupDir "Taix Server.lnk"
$monitorStartupSc = Join-Path $StartupDir "Taix Monitor.lnk"

New-AppShortcut -TargetPath $serverExe  -ShortcutPath $serverStartupSc
Write-Host "[+] Added taix-server to Startup folder"

New-AppShortcut -TargetPath $monitorExe -ShortcutPath $monitorStartupSc
Write-Host "[+] Added taix-monitor-windows to Startup folder"

[System.Runtime.Interopservices.Marshal]::ReleaseComObject($WshShell) | Out-Null

# Optional: start immediately
if ($StartServer) {
    Start-Process -FilePath $serverExe -WorkingDirectory $InstallDir
    Write-Host "[+] Started taix-server"
}

if ($StartMonitor) {
    Start-Process -FilePath $monitorExe -WorkingDirectory $InstallDir
    Write-Host "[+] Started taix-monitor-windows"
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Taix installation complete!"            -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Install directory: $InstallDir"
Write-Host ""
Write-Host "Auto-launch method: Startup folder (zero registry)"
Write-Host "  - Taix Server.lnk   (local API service)"
Write-Host "  - Taix Monitor.lnk  (app usage tracker)"
Write-Host ""
Write-Host "You can view them by typing the following in File Explorer:"
Write-Host "  shell:startup" -ForegroundColor Cyan
Write-Host ""
if (-not $DesktopShortcut) {
    Write-Host "Tip: to also create a Desktop shortcut, run:" -ForegroundColor Cyan
    Write-Host "  .\install.ps1 -DesktopShortcut"               -ForegroundColor Cyan
}
Write-Host ""
Read-Host "Press Enter to exit"
