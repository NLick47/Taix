#Requires -Version 5.1
<#
.SYNOPSIS
    卸载 Taix：删快捷方式、停进程、清任务。

.DESCRIPTION
    删掉开始菜单和桌面的快捷方式，从任务计划注销组件，
    停掉正在跑的进程，清理旧注册表，最后删掉可执行文件。
    用户数据（配置、数据库、日志等）会保留。

    组件配置在最上面的表里，改那儿就行。
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$InstallDir = $PSScriptRoot
if (-not $InstallDir) {
    $InstallDir = (Get-Location).Path
}

# ---- 组件配置表，必须和 install.ps1 保持一致 ----
$Components = @(
    [pscustomobject]@{
        Name       = "Monitor"
        ExeName    = "taix-monitor-windows.exe"
        TaskName   = "TaixMonitor"
        InstallVia = "builtin"
    },
    [pscustomobject]@{
        Name       = "Server"
        ExeName    = "taix-server.exe"
        TaskName   = "TaixServer"
        InstallVia = "taskscheduler"
    }
)

function Unregister-TaskSchedulerJob {
    param([string]$TaskName)
    $proc = Start-Process -FilePath "schtasks.exe" `
        -ArgumentList "/DELETE","/F","/TN","`"$TaskName`"" `
        -Wait -PassThru -NoNewWindow
    # 任务不存在也视为成功
    return ($proc.ExitCode -eq 0)
}

# 删快捷方式
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

# 注销组件
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

# 停掉运行中的进程
$processNames = @("taix-server", "taix-monitor-windows", "Taix")
foreach ($procName in $processNames) {
    $running = Get-Process -Name $procName -ErrorAction SilentlyContinue
    if ($running) {
        $running | Stop-Process -Force -ErrorAction SilentlyContinue
        Write-Host "[+] Stopped process: $procName"
    }
}

# 等句柄释放
Start-Sleep -Seconds 1

# 清理旧注册表
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$oldKeys = @("Taix", "TaixServer", "TaixMonitor")
foreach ($key in $oldKeys) {
    $prop = Get-ItemProperty -Path $regPath -Name $key -ErrorAction SilentlyContinue
    if ($prop) {
        Remove-ItemProperty -Path $regPath -Name $key -Force
        Write-Host "[+] Cleaned legacy registry entry: $key"
    }
}

# 删可执行文件
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
Write-Host "(configs, databases, logs, etc.) were preserved."
Write-Host ""
Read-Host "Press Enter to exit"
