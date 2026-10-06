@echo off
cd /d "%~dp0"
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Probe.ps1" -Config "%~dp0site.json" -DurationSeconds 300
pause
