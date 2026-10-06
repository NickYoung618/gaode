@echo off
cd /d "%~dp0"
pwsh -NoProfile -File "%~dp0Stop.ps1"
pause
