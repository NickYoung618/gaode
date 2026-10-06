@echo off
cd /d "%~dp0"
pwsh -NoProfile -File "%~dp0Start.ps1" %*
pause
