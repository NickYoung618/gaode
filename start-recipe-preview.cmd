@echo off
cd /d "%~dp0"
python -u "%~dp0scripts\start-recipe-preview.py" %*
if errorlevel 1 pause
