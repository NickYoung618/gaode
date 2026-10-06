#requires -Version 7.0
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('preflight','begin','accept','verify','assess','finish')]
    [string]$Action,
    [ValidateSet('specify','plan','tasks','analyze','implement','review','fix')]
    [string]$Phase = 'review'
)
$ErrorActionPreference = 'Stop'
if (-not $env:GAODE_WORKFLOW_PYTHON -or -not $env:GAODE_WORKFLOW_REQUEST) {
    throw '请通过项目dev.ps1启动/恢复工作流，缺少已校验的运行上下文。'
}
# runner chooses RecipeExecution010 from the active feature; all profiles require L.
# assess/finish reparse its current credential, independently of passed=true.
& $env:GAODE_WORKFLOW_PYTHON -B (Join-Path $PSScriptRoot 'runner.py') step --action $Action --phase $Phase
exit $LASTEXITCODE
