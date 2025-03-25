@echo off

del /Q "Taix.exe" 2>nul

rd /S /Q "Log" 2>nul

reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "Taix" /f 2>nul

if exist "Taix.exe" (echo Failed to delete Taix.exe) else (echo Taix.exe deleted)
if exist "Log\" (echo Failed to delete Log folder) else (echo Log folder deleted)
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "Taix" 2>nul && (
    echo Failed to delete registry entry
) || (
    echo Registry entry deleted
)

pause