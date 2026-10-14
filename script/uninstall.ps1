#Requires -Version 5.1

[CmdletBinding()]
param()

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    try {
        Start-Process -FilePath "powershell.exe" `
            -Verb RunAs `
            -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`""
        exit 0
    } catch {
        Write-Host "需要管理员权限才能卸载 Taix，但提升请求被取消。" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
}

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

# 插件进程（taix-plugin-<id>.exe，SDK 侧永不自杀，必须显式杀掉）
$pluginProcs = Get-Process -Name "taix-plugin-*" -ErrorAction SilentlyContinue
if ($pluginProcs) {
    $pluginProcs | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "[+] Stopped plugin processes: $(($pluginProcs | ForEach-Object { $_.Name } | Select-Object -Unique) -join ', ')"
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

# 插件目录与 webui 静态资源：随主程序一起清掉，避免残留被下次安装直接复用
foreach ($dirName in @("plugins", "webui")) {
    $dirPath = Join-Path $InstallDir $dirName
    if (Test-Path $dirPath) {
        try {
            Remove-Item $dirPath -Recurse -Force -ErrorAction Stop
            Write-Host "[+] Removed directory: $dirPath"
        } catch {
            Write-Host "[-] Could not remove: $dirPath (locked by another process)" -ForegroundColor Yellow
        }
    }
}

# 插件系统：Toast AUMID 注销（install.ps1 注册的 Taix.Plugin）
try {
    $aumidKey = "HKCU:\SOFTWARE\Classes\AppUserModelId\Taix.Plugin"
    if (Test-Path $aumidKey) {
        Remove-Item $aumidKey -Force
        Write-Host "[+] Unregistered Toast AUMID: Taix.Plugin"
    }
} catch {
    Write-Host "[*] Failed to unregister Toast AUMID: $_" -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Taix uninstallation complete!"          -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Note: executables have been removed, but user data"
Write-Host "(configs, databases, logs, etc.) were preserved."
Write-Host ""
Read-Host "Press Enter to exit"
