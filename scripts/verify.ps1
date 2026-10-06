#requires -Version 7.0
# Every acceptance profile executes current 010 L before its own fixed set.
# A local verify result is not the independent-process or complete 009 acceptance result.
[CmdletBinding()]
param([ValidateRange(0,600)][int]$WaitSeconds = 600, [ValidateSet('Default009','RecipeExecution010','PlcPolling013')][string]$Profile = "Default009")
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$env:PYTHONUTF8 = '1'
$env:PYTHONDONTWRITEBYTECODE = '1'
$python = if ($env:GAODE_WORKFLOW_PYTHON) { $env:GAODE_WORKFLOW_PYTHON } else { (Get-Command python -ErrorAction Stop).Source }
Push-Location -LiteralPath $root
try {
    & $python -B (Join-Path $root 'scripts/workflow/verify_entry.py') --wait-seconds $WaitSeconds --profile $Profile
    $code = $LASTEXITCODE
} finally {
    Pop-Location
}
if ($code -eq 0) { exit 0 }
exit 1
