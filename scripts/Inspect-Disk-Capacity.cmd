@echo off
title Read-only Disk Capacity Inspection
echo Reading all disk capacities and drive mappings. Administrator approval is required.
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Inspect-Disk-Capacity.ps1"
if errorlevel 1 pause
