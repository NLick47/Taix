#Requires -Version 5.1
<#
.SYNOPSIS
    Uninstall Taix suite: remove shortcuts, stop processes, and delete executables.

.DESCRIPTION
    Removes Start Menu and Desktop shortcuts for Taix, deletes taix-server and
    taix-monitor-windows shortcuts from the Startup folder, stops any running
    Taix processes, and removes the executable files from the install directory.
    User data (config files, databases, logs, etc.) are preserved.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$InstallDir = $PSScriptRoot
if (-not $InstallDir) {
    $InstallDir = (Get-Location).Path
}

$WshShell     = New-Object -ComObject WScript.Shell
$StartMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$StartupDir   = $WshShell.SpecialFolders("Startup")
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($WshShell) | Out-Null

# Remove shortcuts
$shortcuts = @(
    (Join-Path $StartMenuDir "Taix.lnk"),
    (Join-Path $env:USERPROFILE "Desktop\Taix.lnk"),
    (Join-Path $StartupDir "Taix Server.lnk"),
    (Join-Path $StartupDir "Taix Monitor.lnk"),
    # Legacy compatibility
    (Join-Path $StartupDir "Taix.lnk")
)

foreach ($sc in $shortcuts) {
    if (Test-Path $sc) {
        Remove-Item $sc -Force
        Write-Host "[+] Removed: $sc"
    }
}

# Stop running processes
$processNames = @("taix-server", "taix-monitor-windows", "Taix")
foreach ($procName in $processNames) {
    $running = Get-Process -Name $procName -ErrorAction SilentlyContinue
    if ($running) {
        $running | Stop-Process -Force -ErrorAction SilentlyContinue
        Write-Host "[+] Stopped process: $procName"
    }
}

# Wait a moment for file handles to be released
Start-Sleep -Seconds 1

# Clean up legacy registry entries (for users who previously used HKCU Run)
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$oldKeys = @("Taix", "TaixServer", "TaixMonitor")
foreach ($key in $oldKeys) {
    $prop = Get-ItemProperty -Path $regPath -Name $key -ErrorAction SilentlyContinue
    if ($prop) {
        Remove-ItemProperty -Path $regPath -Name $key -Force
        Write-Host "[+] Cleaned legacy registry entry: $key"
    }
}

# Remove executables only (preserve user data)
$exeFiles = @(
    (Join-Path $InstallDir "Taix.exe"),
    (Join-Path $InstallDir "taix-server.exe"),
    (Join-Path $InstallDir "taix-monitor-windows.exe")
)

foreach ($exe in $exeFiles) {
    if (Test-Path $exe) {
        Remove-Item $exe -Force
        Write-Host "[+] Removed executable: $exe"
    }
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Taix uninstallation complete!"          -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Note: executables have been removed, but user data"
Write-Host "(configs, databases, logs, etc.) in the install directory"
Write-Host "were preserved."
Write-Host ""
Read-Host "Press Enter to exit"
