#Requires -Version 5.1

[CmdletBinding()]
param(
    [switch]$DesktopShortcut
)

$ErrorActionPreference = "Stop"

$InstallDir = $PSScriptRoot
if (-not $InstallDir) {
    $InstallDir = (Get-Location).Path
}

$Components = @(
    [pscustomobject]@{
        Name       = "Shell"
        ExeName    = "taix-shell.exe"
        TaskName   = "TaixShell"
        InstallVia = "builtin"
    }
)

$requiredExes = @("taix-shell.exe", "taix-monitor-windows.exe", "taix-server.exe", "Taix.exe")
$missing = @()
foreach ($exe in $requiredExes) {
    $p = Join-Path $InstallDir $exe
    if (-not (Test-Path $p)) {
        $missing += $exe
    }
}
if ($missing.Count -gt 0) {
    Write-Host "Error: the following required files are missing:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "`nPlease make sure you have extracted the full release package." -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

$clientExe       = Join-Path $InstallDir "Taix.exe"
$StartMenuDir    = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$startMenuSc     = Join-Path $StartMenuDir "Taix.lnk"

$WshShell = New-Object -ComObject WScript.Shell
$sc = $WshShell.CreateShortcut($startMenuSc)
$sc.TargetPath       = $clientExe
$sc.WorkingDirectory = $InstallDir
$sc.Save()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($WshShell) | Out-Null
Write-Host "[+] Created Start Menu shortcut: $startMenuSc"

if ($DesktopShortcut) {
    $desktopSc = Join-Path $env:USERPROFILE "Desktop\Taix.lnk"
    $WshShell = New-Object -ComObject WScript.Shell
    $sc = $WshShell.CreateShortcut($desktopSc)
    $sc.TargetPath       = $clientExe
    $sc.WorkingDirectory = $InstallDir
    $sc.Save()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($WshShell) | Out-Null
    Write-Host "[+] Created Desktop shortcut: $desktopSc"
}

foreach ($c in $Components) {
    $exePath = Join-Path $InstallDir $c.ExeName

    if ($c.InstallVia -eq "builtin" -and (Test-Path $exePath)) {
        Write-Host "[+] Registering $($c.TaskName) (via builtin install)..."
        $proc = Start-Process -FilePath $exePath -ArgumentList "install" `
            -WorkingDirectory $InstallDir -Wait -PassThru -NoNewWindow
        if ($proc.ExitCode -eq 0) {
            Write-Host "[+] $($c.TaskName) registered successfully"
        } else {
            Write-Host "Warning: $($c.TaskName) install returned exit code $($proc.ExitCode)" -ForegroundColor Yellow
        }
    }
}

$shellExe = Join-Path $InstallDir "taix-shell.exe"
Write-Host "[+] Starting taix-shell..."
Start-Process -FilePath $shellExe -WorkingDirectory $InstallDir

Start-Sleep -Seconds 2

$running = Get-Process -Name "taix-shell" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[+] Verified taix-shell is running"
} else {
    Write-Host "Warning: taix-shell does not appear to be running" -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Taix installation complete!"            -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Install directory: $InstallDir"
Write-Host ""
Write-Host "Auto-launch configuration:"
Write-Host "  - TaixShell  -> Task Scheduler (via taix-shell install)"
Write-Host ""
if (-not $DesktopShortcut) {
    Write-Host "Tip: to also create a Desktop shortcut, run:" -ForegroundColor Cyan
    Write-Host "  .\install.ps1 -DesktopShortcut"               -ForegroundColor Cyan
}
Write-Host ""
Read-Host "Press Enter to exit"
