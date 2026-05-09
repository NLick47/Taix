#Requires -Version 5.1

[CmdletBinding()]
param()

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

function Unregister-TaskSchedulerJob {
    param([string]$TaskName)
    $proc = Start-Process -FilePath "schtasks.exe" `
        -ArgumentList "/DELETE","/F","/TN","`"$TaskName`"" `
        -Wait -PassThru -NoNewWindow
    return ($proc.ExitCode -eq 0)
}

$WshShell     = New-Object -ComObject WScript.Shell
$StartMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$StartupDir   = $WshShell.SpecialFolders("Startup")
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($WshShell) | Out-Null

$shortcuts = @(
    (Join-Path $StartMenuDir "Taix.lnk"),
    (Join-Path $env:USERPROFILE "Desktop\Taix.lnk")
)

foreach ($sc in $shortcuts) {
    if (Test-Path $sc) {
        try {
            Remove-Item $sc -Force -ErrorAction Stop
            Write-Host "[+] Removed: $sc"
        } catch {
            Write-Host "[-] Could not remove: $sc (locked by another process)" -ForegroundColor Yellow
        }
    }
}

$processNames = @("taix-shell", "taix-server", "taix-monitor-windows", "Taix")
foreach ($procName in $processNames) {
    $running = Get-Process -Name $procName -ErrorAction SilentlyContinue
    if ($running) {
        $running | Stop-Process -Force -ErrorAction SilentlyContinue
        Write-Host "[+] Stopped process: $procName"
    }
}

Start-Sleep -Seconds 1

foreach ($c in $Components) {
    $exePath = Join-Path $InstallDir $c.ExeName

    if ($c.InstallVia -eq "builtin" -and (Test-Path $exePath)) {
        Write-Host "[+] Unregistering $($c.TaskName) (via builtin uninstall)..."
        $proc = Start-Process -FilePath $exePath -ArgumentList "uninstall" `
            -Wait -PassThru -NoNewWindow
        if ($proc.ExitCode -eq 0) {
            Write-Host "[+] $($c.TaskName) unregistered"
        } else {
            Write-Host "Warning: $($c.TaskName) uninstall returned exit code $($proc.ExitCode)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[+] Unregistering $($c.TaskName) from Task Scheduler..."
        if (Unregister-TaskSchedulerJob -TaskName $c.TaskName) {
            Write-Host "[+] $($c.TaskName) unregistered"
        } else {
            Write-Host "Warning: could not unregister $($c.TaskName) (may not exist)" -ForegroundColor Yellow
        }
    }
}

$legacyTasks = @("TaixMonitor", "TaixServer")
foreach ($task in $legacyTasks) {
    $proc = Start-Process -FilePath "schtasks.exe" `
        -ArgumentList "/DELETE","/F","/TN","`"$task`"" `
        -Wait -PassThru -NoNewWindow -ErrorAction SilentlyContinue
    if ($proc.ExitCode -eq 0) {
        Write-Host "[+] Cleaned legacy task: $task"
    }
}

$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$oldKeys = @("Taix", "TaixServer", "TaixMonitor", "TaixShell")
foreach ($key in $oldKeys) {
    $prop = Get-ItemProperty -Path $regPath -Name $key -ErrorAction SilentlyContinue
    if ($prop) {
        Remove-ItemProperty -Path $regPath -Name $key -Force
        Write-Host "[+] Cleaned legacy registry entry: $key"
    }
}

$exeFiles = @(
    (Join-Path $InstallDir "Taix.exe"),
    (Join-Path $InstallDir "taix-shell.exe"),
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
Write-Host "(configs, databases, logs, etc.) were preserved."
Write-Host ""
Read-Host "Press Enter to exit"
