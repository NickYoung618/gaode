@echo off
title E Volume Extension
echo Starting E: volume extension helper...
echo If this is the only message, Windows PowerShell has not reached the script yet.
echo Administrator authorization is required. Check the taskbar for another window.
echo.
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Extend-E-Volume.ps1"
if errorlevel 1 pause
