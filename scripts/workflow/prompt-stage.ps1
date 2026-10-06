#requires -Version 7.0
param(
    [ValidateSet('specify','plan','tasks','analyze','implement','review','fix')]
    [string]$Phase,
    [string]$ProbeFolder
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
if (-not $env:GAODE_WORKFLOW_PYTHON -or (-not $ProbeFolder -and -not $env:GAODE_WORKFLOW_REQUEST)) {
    throw '缺少已校验的Workflow运行上下文。'
}
if ((Get-Process -Id $PID).SessionId -eq 0) {
    $bridge=@('-NoProfile','-File',(Join-Path $PSScriptRoot 'session-bridge.ps1'))
    if($ProbeFolder) { $bridge+=@('-ProbeFolder',$ProbeFolder) } else { $bridge+=@('-Phase',$Phase) }
    & pwsh @bridge
} elseif ($ProbeFolder) {
    & $env:GAODE_WORKFLOW_PYTHON -B (Join-Path $PSScriptRoot 'prompt_stage.py') --probe-folder $ProbeFolder
} else {
    & $env:GAODE_WORKFLOW_PYTHON -B (Join-Path $PSScriptRoot 'prompt_stage.py') --phase $Phase
}
exit $LASTEXITCODE
