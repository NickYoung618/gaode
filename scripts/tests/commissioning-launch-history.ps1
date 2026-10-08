param([string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot)
if(-not $EvidenceRoot){$EvidenceRoot=Join-Path $repo ('artifacts/reset-diagnostic-fix-20261009/launch-'+[Guid]::NewGuid().ToString('N'))}
$testRoot=[IO.Path]::GetFullPath($EvidenceRoot)
if(Test-Path -LiteralPath $testRoot){throw 'Use a new isolated evidence directory.'}
foreach($name in @('scripts','config','data')){[IO.Directory]::CreateDirectory((Join-Path $testRoot $name))|Out-Null}
Copy-Item -LiteralPath (Join-Path $repo 'scripts/Start-CommissioningConsole.ps1') -Destination (Join-Path $testRoot 'scripts')
'{}'|Set-Content -LiteralPath (Join-Path $testRoot 'config/host.json')
'{}'|Set-Content -LiteralPath (Join-Path $testRoot 'config/desktop.json')
@{hostSettings=(Join-Path $testRoot 'config/host.json');desktopProfile=(Join-Path $testRoot 'config/desktop.json');
    fieldConfigurationReviewed=$true;sourceReference='OFFLINE_TEST';apiBaseUrl='http://127.0.0.1:1'}|
    ConvertTo-Json|Set-Content -LiteralPath (Join-Path $testRoot 'config/runtime-profile.json')
'function Wait-CommissioningIdentity { param($ApiBaseUrl,$DesktopProfile,$HostProcessId) $launchProbe.Waits++ }'|
    Set-Content -LiteralPath (Join-Path $testRoot 'scripts/Wait-CommissioningIdentity.ps1')
$launchProbe=[PSCustomObject]@{Starts=0;Waits=0}
# Execute the actual launcher with process and identity boundaries replaced.
# No real Host, desktop, network or hardware is started.
function Start-Process {
    param($FilePath,$WorkingDirectory,$WindowStyle,[switch]$PassThru,$RedirectStandardOutput,$RedirectStandardError)
    $launchProbe.Starts++
    if($RedirectStandardOutput){
        [IO.File]::WriteAllText($RedirectStandardOutput,'current-out-'+$launchProbe.Starts)
        [IO.File]::WriteAllText($RedirectStandardError,'current-error-'+$launchProbe.Starts)
    }
    if($PassThru){[PSCustomObject]@{Id=999999}}
}
$outPath=Join-Path $testRoot 'data/host.stdout.log';$errorPath=Join-Path $testRoot 'data/host.stderr.log'
[IO.File]::WriteAllText($outPath,'original failure stack');[IO.File]::WriteAllText($errorPath,'original stderr')
$launch=Join-Path $testRoot 'scripts/Start-CommissioningConsole.ps1'
$history=Join-Path $testRoot 'data/host-log-history'
& $launch -CheckOnly|Out-Null
if($launchProbe.Starts -ne 0 -or (Test-Path -LiteralPath $history)){throw 'CheckOnly archived or started a process.'}
& $launch -Run|Out-Null
$first=@(Get-ChildItem -LiteralPath $history -Directory)
if($first.Count -ne 1 -or [IO.File]::ReadAllText((Join-Path $first[0].FullName 'host.stdout.log')) -ne 'original failure stack' -or
    [IO.File]::ReadAllText((Join-Path $first[0].FullName 'host.stderr.log')) -ne 'original stderr'){throw 'First failure logs were not preserved.'}
& $launch -Run|Out-Null
$archives=@(Get-ChildItem -LiteralPath $history -Directory)
$second=@($archives|Where-Object FullName -ne $first[0].FullName)
if($archives.Count -ne 2 -or $launchProbe.Starts -ne 4 -or $launchProbe.Waits -ne 2 -or
    [IO.File]::ReadAllText((Join-Path $second[0].FullName 'host.stdout.log')) -ne 'current-out-1' -or
    [IO.File]::ReadAllText($outPath) -ne 'current-out-3'){throw 'Second launch failed to retain both histories and the current log.'}
$copyFailureBlocked=$false
$held=[IO.File]::Open($outPath,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    try { & $launch -Run|Out-Null }
    catch { $copyFailureBlocked=$true }
} finally { $held.Dispose() }
if(-not $copyFailureBlocked -or $launchProbe.Starts -ne 4 -or [IO.File]::ReadAllText($outPath) -ne 'current-out-3'){
    throw 'Failed archival did not prevent starting/truncating.'
}
@{environment='OFFLINE_PROCESS_STUBS';checkOnlyPreserved=$true;twoLaunchesPreserved=$true;
    copyFailureBlocked=$copyFailureBlocked;realProcessesStarted=0;deviceDispatches=0;root=$testRoot}|
    ConvertTo-Json|Tee-Object -FilePath (Join-Path $testRoot 'result.json')
