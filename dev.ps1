#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Position=0)][string]$Requirement,
    [ValidateSet('New','Continue')][string]$Mode = 'New',
    [string]$Feature,
    [ValidateSet('M1','M2','All')][string]$Milestone = 'M1',
    [string]$Resume,
    [switch]$Check,
    [switch]$Doctor,
    [switch]$Status,
    [switch]$WorkspaceIdle
)
$ErrorActionPreference = 'Stop'
$runner = Join-Path $PSScriptRoot 'scripts/workflow/runner.py'
$uv = Get-Command uv -ErrorAction SilentlyContinue
if (-not $uv) { throw '未找到uv。此入口使用已安装specify-cli的Python环境，不自动安装工具。' }
$toolRoot = (& $uv.Source tool dir | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw '无法定位uv工具目录。' }
$python = Join-Path $toolRoot 'specify-cli/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $python -PathType Leaf)) {
    throw "未找到Spec Kit Python环境：$python"
}
$runArgs = @('-B', $runner)
if ($Doctor) { $runArgs = @('-B',(Join-Path $PSScriptRoot 'scripts/workflow/doctor.py')) }
elseif ($Check) { $runArgs += 'check' }
elseif ($Status) { $runArgs += 'status' }
elseif ($Resume) {
    $runArgs += @('resume', '--run-id', $Resume)
    if ($WorkspaceIdle) { $runArgs += '--workspace-idle' }
}
elseif ($Requirement -or $Mode -eq 'Continue') {
    $runArgs += @('start', '--mode', $Mode.ToLowerInvariant(), '--milestone', $Milestone)
    if ($Requirement) { $runArgs += @('--requirement', $Requirement) }
    if ($Feature) { $runArgs += @('--feature', $Feature) }
    if ($WorkspaceIdle) { $runArgs += '--workspace-idle' }
}
else {
    Write-Output @'
检查配置（不启动开发）： .\dev.ps1 -Check
执行隔离链路探针：       .\dev.ps1 -Doctor
查看运行记录：           .\dev.ps1 -Status
新需求：                 .\dev.ps1 "需求" -Feature 002-feature-name -WorkspaceIdle
接续第一工位M1：         .\dev.ps1 -Mode Continue -Milestone M1 -WorkspaceIdle
接续第一工位M2：         .\dev.ps1 -Mode Continue -Milestone M2 -WorkspaceIdle
恢复Workflow：           .\dev.ps1 -Resume <run_id> -WorkspaceIdle
并行隔离工作区：         .\dev-parallel.ps1 -WorkspaceName <name> -Feature <unique-feature> -Requirement <需求>
WorkspaceIdle表示你已确认其他开发会话停止写入本项目；本入口不接管正在运行的会话。
'@
    exit 0
}
$env:PYTHONUTF8='1'
$env:PYTHONDONTWRITEBYTECODE='1'
& $python @runArgs
exit $LASTEXITCODE
