@echo off
cd /d "%~dp0"
pwsh -NoProfile -File "%~dp0Select-Test.ps1"
pause
