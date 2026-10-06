@echo off
cd /d "%~dp0"
python -u "%~dp0verify_migration.py" "%~dp0."
set "verify_result=%ERRORLEVEL%"
if not "%verify_result%"=="0" echo Verification failed. Read the error above.
pause
exit /b %verify_result%
