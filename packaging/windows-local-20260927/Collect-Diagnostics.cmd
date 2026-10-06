@echo off
cd /d "%~dp0"
pwsh -NoProfile -File "%~dp0Collect-Diagnostics.ps1"
pause
