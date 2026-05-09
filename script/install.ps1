#Requires -Version 5.1
<#
.SYNOPSIS
    安装 Taix：创建快捷方式并注册任务计划。

.DESCRIPTION
    给客户端创建开始菜单快捷方式，把 monitor 和 server 注册成
    任务计划实现开机自启，装完直接启动它们。

    要加新组件的话，改一下最上面那张表就行。

.PARAMETER DesktopShortcut
    顺便在桌面也建个快捷方式。
#>

[CmdletBinding()]
param(
    [switch]$DesktopShortcut
)

$ErrorActionPreference = "Stop"

$InstallDir = $PSScriptRoot
if (-not $InstallDir) {
    $InstallDir = (Get-Location).Path
}

# ---- 组件配置表，新增组件往下加就行 ----
$Components = @(
    [pscustomobject]@{
        Name       = "Monitor"
        ExeName    = "taix-monitor-windows.exe"
        TaskName   = "TaixMonitor"
        InstallVia = "taskscheduler"  # 通过通用 XML 注册
    },
    [pscustomobject]@{
        Name       = "Server"
        ExeName    = "taix-server.exe"
        TaskName   = "TaixServer"
        InstallVia = "taskscheduler"  # 通过通用 XML 注册
        DelaySec   = 10
    }
)

function Register-TaskSchedulerJob {
    param(
        [string]$TaskName,
        [string]$ExePath,
        [string]$Arguments = "",
        [string]$WorkingDirectory = $InstallDir,
        [int]$DelaySeconds = 30
    )

    $xml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Description>$([System.Security.SecurityElement]::Escape($TaskName)) auto-launch task</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <Delay>PT${DelaySeconds}S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>true</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <RestartOnFailure>
      <Interval>PT1M</Interval>
      <Count>999</Count>
    </RestartOnFailure>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>$([System.Security.SecurityElement]::Escape($ExePath))</Command>
      <Arguments>$([System.Security.SecurityElement]::Escape($Arguments))</Arguments>
      <WorkingDirectory>$([System.Security.SecurityElement]::Escape($WorkingDirectory))</WorkingDirectory>
    </Exec>
  </Actions>
</Task>
"@

    $tempXml = Join-Path $env:TEMP "$TaskName.xml"
    $bytes     = [System.Text.Encoding]::Unicode.GetBytes($xml)
    $bom       = [byte[]]@(0xFF, 0xFE)
    $allBytes  = $bom + $bytes
    [System.IO.File]::WriteAllBytes($tempXml, $allBytes)

    $proc = Start-Process -FilePath "schtasks.exe" `
        -ArgumentList "/CREATE","/XML","`"$tempXml`"","/TN","`"$TaskName`"" `
        -Wait -PassThru -NoNewWindow

    Remove-Item $tempXml -Force -ErrorAction SilentlyContinue

    if ($proc.ExitCode -ne 0) {
        throw "schtasks.exe failed with exit code $($proc.ExitCode)"
    }
}

# 检查可执行文件有没有缺
$missing = @()
foreach ($c in $Components) {
    $p = Join-Path $InstallDir $c.ExeName
    if (-not (Test-Path $p)) {
        $missing += $c.ExeName
    }
}
if ($missing.Count -gt 0) {
    Write-Host "Error: the following required files are missing:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "`nPlease make sure you have extracted the full release package." -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# 客户端快捷方式
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

# 注册组件
foreach ($c in $Components) {
    $exePath = Join-Path $InstallDir $c.ExeName

    # 覆盖安装时先把旧任务清掉，不然 schtasks 会报错
    $null = Start-Process -FilePath "schtasks.exe" `
        -ArgumentList "/DELETE","/F","/TN","`"$($c.TaskName)`"" `
        -Wait -PassThru -NoNewWindow -ErrorAction SilentlyContinue

    if ($c.InstallVia -eq "builtin") {
        Write-Host "[+] Registering $($c.TaskName) (via builtin install)..."
        $proc = Start-Process -FilePath $exePath -ArgumentList "install" `
            -WorkingDirectory $InstallDir -Wait -PassThru -NoNewWindow
        if ($proc.ExitCode -eq 0) {
            Write-Host "[+] $($c.TaskName) registered successfully"
        } else {
            Write-Host "Warning: $($c.TaskName) install returned exit code $($proc.ExitCode)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[+] Registering $($c.TaskName) (via Task Scheduler)..."
        Register-TaskSchedulerJob -TaskName $c.TaskName -ExePath $exePath -DelaySeconds $c.DelaySec
        Write-Host "[+] $($c.TaskName) registered successfully"
    }
}

# 启动组件
foreach ($c in $Components) {
    $exePath = Join-Path $InstallDir $c.ExeName
    Write-Host "[+] Starting $($c.Name)..."
    Start-Process -FilePath $exePath -WorkingDirectory $InstallDir
}

# 等几秒让进程起来
Start-Sleep -Seconds 2

# 看看进程跑了没
foreach ($c in $Components) {
    $procName = [System.IO.Path]::GetFileNameWithoutExtension($c.ExeName)
    $running = Get-Process -Name $procName -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "[+] Verified $($c.Name) is running"
    } else {
        Write-Host "Warning: $($c.Name) does not appear to be running" -ForegroundColor Yellow
    }
}

# 完工
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Taix installation complete!"            -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Install directory: $InstallDir"
Write-Host ""
Write-Host "Auto-launch configuration:"
foreach ($c in $Components) {
    Write-Host "  - $($c.TaskName)  -> Task Scheduler ($($c.InstallVia))"
}
Write-Host ""
if (-not $DesktopShortcut) {
    Write-Host "Tip: to also create a Desktop shortcut, run:" -ForegroundColor Cyan
    Write-Host "  .\install.ps1 -DesktopShortcut"               -ForegroundColor Cyan
}
Write-Host ""
Read-Host "Press Enter to exit"
