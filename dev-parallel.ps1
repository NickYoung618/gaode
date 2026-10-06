#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$WorkspaceName,
    [Parameter(Position=0)][string]$Requirement,
    [string]$Feature,
    [ValidateSet('New','Continue')][string]$Mode = 'New',
    [ValidateSet('M1','M2','All')][string]$Milestone = 'M1',
    [string]$Resume,
    [string]$WorkspaceParent,
    [switch]$Reuse,
    [switch]$SourceIdle,
    [switch]$WorkspaceIdle,
    [switch]$CreateOnly,
    [switch]$Check,
    [switch]$Doctor,
    [switch]$Verify,
    [switch]$Status,
    # Verify waits for the server-wide verification seat by default; callers
    # can set 0 for fail-fast behavior.
    [ValidateRange(0,600)][int]$WaitSeconds = 600
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$OutputEncoding=[Text.UTF8Encoding]::new($false)
$toolRoot=(& uv tool dir | Out-String).Trim()
if($LASTEXITCODE -ne 0) { throw '无法定位uv工具目录' }
$python=Join-Path $toolRoot 'specify-cli/Scripts/python.exe'
if(-not (Test-Path -LiteralPath $python -PathType Leaf)) { throw '未找到Spec Kit Python环境' }
$arguments=@('-B',(Join-Path $PSScriptRoot 'scripts/workflow/workspaces.py'),
    '--name',$WorkspaceName,'--mode',$Mode.ToLowerInvariant(),'--milestone',$Milestone,
    '--wait-seconds',[string]$WaitSeconds)
foreach($pair in @(@('--requirement',$Requirement),@('--feature',$Feature),
                  @('--resume',$Resume),@('--parent',$WorkspaceParent))) {
    if($pair[1]) { $arguments+=@($pair[0],$pair[1]) }
}
foreach($pair in @(@('--reuse',$Reuse),@('--source-idle',$SourceIdle),
                  @('--workspace-idle',$WorkspaceIdle),@('--create-only',$CreateOnly),
                  @('--check',$Check),@('--doctor',$Doctor),@('--verify',$Verify),@('--status',$Status))) {
    if($pair[1]) { $arguments+=$pair[0] }
}
$env:PYTHONUTF8='1'
$env:PYTHONDONTWRITEBYTECODE='1'
& $python @arguments
exit $LASTEXITCODE
