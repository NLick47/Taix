@echo off
setlocal enabledelayedexpansion

set "BAT_DIR=%~dp0"


set "TARGET_EXE=%BAT_DIR%Taix.exe"


if not exist "%TARGET_EXE%" (
    echo error：not found Taix.exe！
    pause
    exit /b
)

set "USERPROFILE_PATH=%USERPROFILE%"


set "SHORTCUT_NAME=Taix.lnk"

set "DEST_DIR1=%USERPROFILE_PATH%\AppData\Roaming\Microsoft\Windows\Start Menu\Programs"
mshta VBScript:Execute("Set ws=CreateObject(""WScript.Shell""):Set sc=ws.CreateShortcut(""%DEST_DIR1%\%SHORTCUT_NAME%""):sc.TargetPath=""%TARGET_EXE%"":sc.WorkingDirectory=""%BAT_DIR%"":sc.Save:close")


set "DEST_DIR2=%USERPROFILE_PATH%\Desktop"
mshta VBScript:Execute("Set ws=CreateObject(""WScript.Shell""):Set sc=ws.CreateShortcut(""%DEST_DIR2%\%SHORTCUT_NAME%""):sc.TargetPath=""%TARGET_EXE%"":sc.WorkingDirectory=""%BAT_DIR%"":sc.Save:close")

echo A shortcut has been created：
echo [start menu] %DEST_DIR1%\
echo [desktop]      %DEST_DIR2%\
pause