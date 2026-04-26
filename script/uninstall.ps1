#Requires -Version 5.1
<#
.SYNOPSIS
    Uninstall Taix suite: remove shortcuts and Startup folder items.

.DESCRIPTION
    Removes Start Menu and Desktop shortcuts for Taix, and deletes
    taix-server and taix-monitor-windows shortcuts from the Startup folder.
    Does NOT delete the program files themselves.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

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

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Taix uninstallation complete!"          -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Note: program files were NOT deleted."
Write-Host "To fully remove, delete the install directory manually."
Write-Host ""
Read-Host "Press Enter to exit"
