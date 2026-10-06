#requires -Version 7.0
param([Parameter(Mandatory)][string]$Folder)
$ErrorActionPreference='Stop'
if(-not $env:GAODE_WORKFLOW_PYTHON) { throw '缺少Workflow Python路径' }
& $env:GAODE_WORKFLOW_PYTHON -B (Join-Path $PSScriptRoot 'doctor.py') --accept $Folder
exit $LASTEXITCODE
