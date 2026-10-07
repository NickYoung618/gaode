@echo off
setlocal
"%SystemRoot%\System32\chcp.com" 65001 >nul
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Stop-PLC.ps1"
if errorlevel 1 pause
