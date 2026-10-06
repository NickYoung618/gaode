@echo off
setlocal
cd /d "%~dp0.."

where pwsh >nul 2>&1
if errorlevel 1 (
    echo PowerShell 7 ^(pwsh^) was not found.
    pause
    exit /b 1
)

pwsh -NoProfile -File "%~dp0stop-station01-manual-test.ps1"
set "result=%errorlevel%"
if not "%result%"=="0" (
    echo.
    echo Stop was incomplete. Read the details above before closing this window.
) else (
    echo.
    echo Recorded test processes stopped and test ports released.
)
pause
exit /b %result%
