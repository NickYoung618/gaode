# Both Python consumers run current 010 L before their own fixed scope and recheck before success.
param(
    [Parameter(Mandatory)][ValidateSet('Baseline','Gates','Mutation','BoundaryMinimum')][string]$Mode,
    [ValidateSet('M01','M02','M03','M04','M05')][string]$Variant,
    [string]$FixtureManifest,
    [string]$FreezeManifest,
    [string]$HostDll,
    [string]$PlcDll,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [string[]]$Cases=@(),
    [switch]$WindowsNativeThreadPool
)
$ErrorActionPreference='Stop'
if ($Mode -eq 'BoundaryMinimum') {
    if ($Variant -or $FixtureManifest -or $FreezeManifest -or $HostDll -or $PlcDll -or $Cases.Count -or $WindowsNativeThreadPool) {
        throw 'BoundaryMinimum has a fixed case set and does not accept dynamic, binary override or filter options.'
    }
    $env:PYTHONUTF8='1'
    $env:PYTHONDONTWRITEBYTECODE='1'
    $boundaryPython=if ($env:GAODE_WORKFLOW_PYTHON) { $env:GAODE_WORKFLOW_PYTHON } else { (Get-Command python -ErrorAction Stop).Source }
    & $boundaryPython -B (Join-Path $PSScriptRoot 'workflow/boundary_minimum.py') --evidence $EvidenceRoot
    if ($LASTEXITCODE -ne 0) { throw 'BoundaryMinimum rejected; inspect the current result. Complete dynamic acceptance was not run.' }
    return
}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $HostDll) { $HostDll=Join-Path $repo 'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll' }
if (-not $PlcDll) { $PlcDll=Join-Path $repo 'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll' }
$arguments=@('-B',(Join-Path $PSScriptRoot 'workflow/protocol_isolation.py'),'--mode',$Mode,'--evidence',$EvidenceRoot,'--host',$HostDll,'--plc',$PlcDll)
if ($Variant) { $arguments+=@('--variant',$Variant) }
if ($FreezeManifest) { $arguments+=@('--freeze',$FreezeManifest) }
if ($FixtureManifest) { $arguments+=@('--fixture',$FixtureManifest) }
foreach ($case in @($Cases | ForEach-Object { $_ -split ',' } | Where-Object { $_ })) { $arguments+=@('--case',$case) }
if ($WindowsNativeThreadPool) { $arguments+='--windows-native-thread-pool' }
$env:PYTHONUTF8='1'
$env:PYTHONDONTWRITEBYTECODE='1'
$workflowPython=if ($env:GAODE_WORKFLOW_PYTHON) { $env:GAODE_WORKFLOW_PYTHON } else { (Get-Command python -ErrorAction Stop).Source }
& $workflowPython @arguments
if ($LASTEXITCODE -ne 0) { throw '009 verification incomplete or rejected; inspect this run result.' }
