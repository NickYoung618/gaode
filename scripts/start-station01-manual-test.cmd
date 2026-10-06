@echo off
setlocal
cd /d "%~dp0.."

where pwsh >nul 2>&1
if errorlevel 1 (
    echo PowerShell 7 ^(pwsh^) was not found. Install it, then run this file again.
    pause
    exit /b 1
)

pwsh -NoProfile -File "%~dp0start-station01-manual-test.ps1"
set "result=%errorlevel%"
if not "%result%"=="0" (
    echo.
    echo Startup failed. Read the error above before closing this window.
) else (
    echo.
    echo Startup command finished. Use the opened desktop page to start the test run.
)
pause
exit /b %result%
