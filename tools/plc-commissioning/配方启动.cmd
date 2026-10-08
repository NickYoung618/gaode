@echo off
setlocal
"%SystemRoot%\System32\chcp.com" 65001 >nul
title 模拟翻面件-001 配方界面
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-PLC.ps1" -View recipe
set "PLC_EXIT_CODE=%ERRORLEVEL%"
if not "%PLC_EXIT_CODE%"=="0" pause
exit /b %PLC_EXIT_CODE%
