$ErrorActionPreference='Stop'
$work='E:\dzk\gaode-1\artifacts\windows-package-20260927T052041Z'
$root=Join-Path $work 'Gaode-008-Windows'
$manifest=Get-Content (Join-Path $root 'package-manifest.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($item in $manifest.files.GetEnumerator()) {
    if ((Get-FileHash (Join-Path $root $item.Key)).Hash -ne $item.Value) { throw "Package changed: $($item.Key)" }
}
$result=[ordered]@{startedUtc=[datetime]::UtcNow.ToString('o');package=$root;scope='Relocated package startup, true component readiness and owned cleanup; no new full Q01 run';passed=$false}
try {
    # Complete verification of the already-started owned package. Do not start it twice.
    $result.reusedStartupRequest='3efe46b593a547a7a1c22a6dfb0eb154'
    $marker=Get-Content (Join-Path $root 'artifacts/recipe-execution-008/local-active.json') -Raw | ConvertFrom-Json
    $record=Get-Content $marker.processFile -Raw | ConvertFrom-Json
    $result.process=$record
    if (-not $record.configuration.windowsNativeThreadPool -or -not $record.hostStatus.plc.connected -or $record.hostStatus.camera.state -ne 'Ready' -or $record.hostStatus.algorithm.state -ne 'Ready') { throw 'Actual component readiness failed' }
    $result.plcHealth=Invoke-RestMethod 'http://127.0.0.1:25180/health' -TimeoutSec 5
    $result.dashboardStatus=(Invoke-WebRequest 'http://127.0.0.1:25180/' -TimeoutSec 5).StatusCode
    $result.databaseBytes=(Get-Item (Join-Path $record.testRoot 'station01.test.db')).Length
    $result.preparedLoadExists=Test-Path $record.preparedLoad
    $result.runtime=& dotnet --list-runtimes
    $result.passed=$result.databaseBytes -gt 0 -and $result.preparedLoadExists -and $result.dashboardStatus -eq 200
} catch { $result.error=$_.ToString() }
finally {
    $markerPath=Join-Path $root 'artifacts/recipe-execution-008/local-active.json'
    if ((Test-Path $markerPath) -and (Get-Content $markerPath -Raw | ConvertFrom-Json).state -eq 'Active') {
        & (Join-Path $root 'Stop.ps1') *> (Join-Path $work 'stop-validation.log')
    }
    $ownedRemaining=@()
    if ($record) { foreach ($key in 'hostPid','plcPid','workerPid') { if(Get-Process -Id $record.$key -ErrorAction SilentlyContinue) { $ownedRemaining+=$record.$key } } }
    $result.ownedRemaining=$ownedRemaining
    $result.listenersRemaining=@([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() | Where-Object Port -in @(25101,25180,21502))
    $result.passed=$result.passed -and $ownedRemaining.Count -eq 0 -and $result.listenersRemaining.Count -eq 0
    $result.finishedUtc=[datetime]::UtcNow.ToString('o')
    $result | ConvertTo-Json -Depth 24 | Set-Content (Join-Path $work 'package-validation.json') -Encoding utf8
}
if (-not $result.passed) { throw 'Package validation failed; see package-validation.json' }
