@echo off
pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Reset-CommissioningConsole.ps1" -InstallationRoot "%~dp0."
pause
