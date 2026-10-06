param(
    [Parameter(Mandatory)][string]$CaseId,
    [Parameter(Mandatory)][string]$FixtureManifest,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [Parameter(Mandatory)][string]$TestRoot,
    [Parameter(Mandatory)][string]$HostDll,
    [Parameter(Mandatory)][string]$PlcDll,
    [ValidateSet('', 'F05-A','F05-B','F05-C','F06-A','F06-B','F06-C','BA04-late-bound','BA04-late-handoff','BA06-before-port','BA06-during-save','BA06-before-next','BA02-api-input')]
    [string]$PersistenceFault = '',
    [switch]$WindowsNativeThreadPool
)
# Finite orchestration and collection only. Separate business/wire readers decide
# acceptance after the owned processes stop; exit zero here is not a case pass.
$ErrorActionPreference = 'Stop'
if ($CaseId -in @('F04-reset','F01-missing','F01-old-action','L08')) { throw 'RetiredHandshake: current capture/axis protection is verified by 011 communication components; historical evidence remains unchanged.' }
$PSDefaultParameterValues['Invoke-RestMethod:TimeoutSec'] = 10
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = [IO.Path]::GetFullPath($EvidenceRoot)
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/recipe-execution-008/009-isolation'))
if (-not $root.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    (Test-Path -LiteralPath $root)) { throw 'New009EvidenceRootRequired' }
$null = New-Item -ItemType Directory -Path $root
$api='http://127.0.0.1:25302'; $plcApi='http://127.0.0.1:25303'
$token=[guid]::NewGuid().ToString('N'); $headers=@{Authorization="Bearer $token"}
$engineerToken=[guid]::NewGuid().ToString('N'); $engineerHeaders=@{Authorization="Bearer $engineerToken"}
$owned=[Collections.Generic.List[object]]::new()
$phase='start'; $runId=$null; $faultRunId=$null; $errorText=$null; $cleanup=@()
function Save-New([string]$Path,$Value) {
    $data=$Value | ConvertTo-Json -Depth 100
    $stream=[IO.File]::Open($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write)
    try { $bytes=[Text.Encoding]::UTF8.GetBytes($data); $stream.Write($bytes) } finally { $stream.Dispose() }
}
function Publish-Control([string]$Name,$Value) {
    $temporary=Join-Path $TestRoot ($Name+'.'+[guid]::NewGuid().ToString('N')+'.pending')
    Save-New $temporary $Value
    [IO.File]::Move($temporary,(Join-Path $TestRoot $Name),$false)
}
function Stop-OwnedRecorded($item) {
    $process=Get-Process -Id $item.id -ErrorAction SilentlyContinue
    if (-not $process) { return }
    if ($process.Path -ne $item.path -or $process.StartTime.ToUniversalTime().Ticks -ne $item.start) { throw 'OwnedProcessIdentityChanged' }
    # Worker may exit as soon as its Host closes the pipe. Use the verified
    # process object; an already-exited owned process is a completed cleanup.
    try { $process.Kill() } catch { if (-not $process.HasExited) { throw } }
    if (-not $process.WaitForExit(10000)) { throw 'OwnedProcessDidNotExit' }
}
function Read-Changes {
    $first=Invoke-RestMethod "$plcApi/api/simulator/changes?after=0"
    $target=[long]$first.latestSequence; $cursor=0L; $page=$first
    $changes=[Collections.Generic.List[object]]::new()
    do {
        if ($page.gap) { throw 'IncompletePlcChangeExport' }
        $before=$cursor
        foreach ($item in @($page.changes)) {
            if ([long]$item.sequence -gt $target) { break }
            if ([long]$item.sequence -ne $cursor+1) { throw 'PlcChangeCursorGap' }
            $changes.Add($item); $cursor=[long]$item.sequence
        }
        if ($cursor -ge $target) { break }
        if ($cursor -eq $before) { throw 'PlcChangeCursorStalled' }
        $page=Invoke-RestMethod "$plcApi/api/simulator/changes?after=$cursor"
    } while ($cursor -lt $target)
    return @{changes=$changes.ToArray();gap=$false;oldestSequence=$first.oldestSequence;latestSequence=$target}
}
try {
    $parameters=@{OperatorToken=$token;TestRoot=$TestRoot;ApiBase=$api;PlcApiBase=$plcApi;PlcPort=25304;
        HostDll=$HostDll;PlcDll=$PlcDll;FixtureManifest=$FixtureManifest;SkipDesktop=$true;
        WindowsNativeThreadPool=[bool]$WindowsNativeThreadPool;TestPersistenceFaultCase=$PersistenceFault}
    if ($CaseId -eq 'L10') { $parameters.TestPageAdministratorToken=$engineerToken }
    $null=& (Join-Path $PSScriptRoot 'start-station01-virtual-loop.ps1') @parameters
    $platform=Get-Content -LiteralPath (Join-Path $TestRoot 'process.json') -Raw | ConvertFrom-Json
    foreach ($key in @('hostPid','workerPid','plcPid')) {
        $process=Get-Process -Id ([int]$platform.$key) -ErrorAction Stop
        $owned.Add(@{key=$key;id=$process.Id;path=$process.Path;start=$process.StartTime.ToUniversalTime().Ticks})
    }
    Save-New (Join-Path $root 'owned-processes.json') $owned.ToArray()
    $phase='prepare'
    $prepared=& (Join-Path $PSScriptRoot 'simulate-station01-load.ps1') -PrepareOnly -OutputDirectory $root -ApiBase $api -FixtureManifest $FixtureManifest
    $load=Get-Content -LiteralPath $prepared -Raw | ConvertFrom-Json
    if ($CaseId -eq 'L10') {
        Save-New (Join-Path $root 'initial-fault-request.json') (Invoke-RestMethod -Method Post "$plcApi/api/simulator/faults/MoveTimeout")
    }
    $deviceFaults=@{
        'F01-stale'='AxisResponseDelayed';
        'F02-position'='SortingPositionMismatch';'F02-face'='FlipFaceMismatch';'F03-uncertain'='AxisWriteResponseLost';
        'F04-handshake'='FlipAckHold'
    }
    if ($deviceFaults.ContainsKey($CaseId)) {
        $injected=Invoke-RestMethod -Method Post "$plcApi/api/simulator/faults/$($deviceFaults[$CaseId])"
        Save-New (Join-Path $root 'initial-fault-request.json') @{caseId=$CaseId;fault=$deviceFaults[$CaseId];receipt=$injected;atUtc=[datetime]::UtcNow.ToString('o')}
        if (-not $injected.success) { throw 'ControlledDeviceFaultNotAccepted' }
    }
    $phase='start-run'
    if ($CaseId -in @('BA06-downstream/legacy-PROCESS','BA06-downstream/api-none-PROCESS')) {
        # The existing legacy entry has no expected recipe reference. All actual
        # F recognition, configuration, recipe and device work remain required.
        $legacy=$load.request.contextJson | ConvertFrom-Json -AsHashtable
        $legacy.Remove('expectedRecipeRef')
        $legacy.schemaVersion='station01-start-run-context/1.0'
        $load.request.contextJson=$legacy | ConvertTo-Json -Depth 30 -Compress
    }
    Save-New (Join-Path $root 'actual-start-request.json') $load.request
    $receipt=Invoke-RestMethod -Method Post "$api/api/v1/station01/runs" -Headers $headers -ContentType 'application/json' -Body ($load.request | ConvertTo-Json -Depth 30 -Compress)
    $runId=[string]$receipt.runId
    Save-New (Join-Path $root 'receipt.json') $receipt
    if ($PersistenceFault) {
        $arm=@{caseId=$PersistenceFault;runId=$runId;nonce=[guid]::NewGuid().ToString()}
        Publish-Control '009-fault-arm.json' $arm
    }
    $phase='observe'
    # Harness bound only; every product budget remains in the frozen configuration.
    $watch=[Diagnostics.Stopwatch]::StartNew(); $index=0; $manualConfirmed=$false; $cancelSent=$false; $independentBound=$false; $faultReleased=$false
    do {
        Start-Sleep -Milliseconds 500
        try { $run=Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $headers }
        catch {
            if ($PersistenceFault -notin @('F05-C','F06-C')) { throw }
            $index++
            Save-New (Join-Path $root ('query-unavailable-{0:d4}.json' -f $index)) @{
                atUtc=[datetime]::UtcNow.ToString('o');error=$_.Exception.ToString();actualCommit='NotInferredFromQueryFailure'}
            continue
        }
        $index++
        Save-New (Join-Path $root ('observation-{0:d4}.json' -f $index)) @{atUtc=[datetime]::UtcNow.ToString('o');run=$run}
        if ($CaseId -eq 'L10' -and $run.errorCode -and -not $faultRunId) {
            $faultRunId=$runId
            Save-New (Join-Path $root 'fault-run-before-reset.json') $run
            Save-New (Join-Path $root 'failed-command-prompt.json') (Invoke-RestMethod "$api/api/v1/station01/runs/$runId/failed-command-recovery" -Headers $engineerHeaders)
            $current=Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $engineerHeaders
            Save-New (Join-Path $root 'recovery-reset-receipt.json') (Invoke-RestMethod -Method Post "$api/api/v1/station01/runs/$runId/recovery-reset" -Headers $engineerHeaders -ContentType 'application/json' -Body (
                @{requestId=[guid]::NewGuid().ToString();expectedRevision=$current.observedRevision;reason='009 controlled Test fault; reset both ends before a complete new run'} | ConvertTo-Json -Compress))
            $current=Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $engineerHeaders
            Save-New (Join-Path $root 'recovery-check-receipt.json') (Invoke-RestMethod -Method Post "$api/api/v1/station01/runs/$runId/recovery-checks" -Headers $engineerHeaders -ContentType 'application/json' -Body (
                @{requestId=[guid]::NewGuid().ToString();expectedRevision=$current.observedRevision;sameTray=$true;loadingUnchanged=$true;snapshotStillApplicable=$true;
                  reason='Same controlled Test load and frozen inputs retained; formal initial observation must pass';evidenceRefs=@($prepared,$FixtureManifest)} | ConvertTo-Json -Compress))
            $current=Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $engineerHeaders
            if ($current.faultRestart.status -ne 'InitialReady') { throw 'RecoveryInitialStateNotApproved' }
            $nextRequest=$load.request | ConvertTo-Json -Depth 30 | ConvertFrom-Json -AsHashtable
            $nextRequest.requestId='009-explicit-new-run-'+[guid]::NewGuid().ToString('N')
            $nextRequest.restartFrom=@{faultRunId=$faultRunId;resetId=$current.faultRestart.resetId;initialCheckId=$current.faultRestart.initialCheckId;expectedFaultRevision=$current.observedRevision}
            Save-New (Join-Path $root 'explicit-new-run-request.json') $nextRequest
            $next=Invoke-RestMethod -Method Post "$api/api/v1/station01/runs" -Headers $engineerHeaders -ContentType 'application/json' -Body ($nextRequest | ConvertTo-Json -Depth 30 -Compress)
            Save-New (Join-Path $root 'fault-restart-receipt.json') @{receipt=$next;atUtc=[datetime]::UtcNow.ToString('o')}
            $runId=[string]$next.runId
            continue
        }
        if ($PersistenceFault -eq 'BA02-api-input' -and -not $independentBound) {
            $faultPath=Join-Path $TestRoot '009-fault-events.jsonl'
            if (Test-Path -LiteralPath $faultPath) {
                $waiting=@(Get-Content -LiteralPath $faultPath | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object phase -eq 'ExistingHandoffInputHeld')
                if ($waiting.Count) {
                    $fixture=Get-Content -LiteralPath $FixtureManifest -Raw | ConvertFrom-Json
                    # The polling response can precede the held boundary. Query
                    # again after observing it, before the separate API request.
                    $run=Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $headers
                    Save-New (Join-Path $root 'before-independent-binding.json') @{
                        atUtc=[datetime]::UtcNow.ToString('o');run=$run;
                        evidence=(Invoke-RestMethod "$api/api/v1/station01/runs/$runId/evidence" -Headers $headers)}
                    $bindRequest=@{runId=$runId;scenarioId=$fixture.scenarioId;occupiedSlots=$fixture.occupiedSlots}
                    Save-New (Join-Path $root 'independent-binding-request.json') $bindRequest
                    $apiReceipt=Invoke-RestMethod -Method Post "$api/api/v1/recipes/bind" -Headers $headers -ContentType 'application/json' -TimeoutSec 20 -Body ($bindRequest | ConvertTo-Json -Depth 10 -Compress)
                    Save-New (Join-Path $root 'independent-binding-response.json') @{atUtc=[datetime]::UtcNow.ToString('o');receipt=$apiReceipt}
                    $current=Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $headers
                    Save-New (Join-Path $root 'after-independent-binding.json') @{
                        atUtc=[datetime]::UtcNow.ToString('o');run=$current;
                        evidence=(Invoke-RestMethod "$api/api/v1/station01/runs/$runId/evidence" -Headers $headers)}
                    # End the held original request using its formal cancellation
                    # endpoint. The independent receipt authorizes no product replay.
                    $cancelReceipt=Invoke-RestMethod -Method Post "$api/api/v1/station01/runs/$runId/cancel" -Headers $headers -ContentType 'application/json' -Body (
                        @{requestId=[guid]::NewGuid().ToString();expectedRevision=$current.observedRevision;reason='009 independent binding evidence collected; end original held Test request'} | ConvertTo-Json -Compress)
                    Save-New (Join-Path $root 'cancel-receipt.json') @{atUtc=[datetime]::UtcNow.ToString('o');receipt=$cancelReceipt}
                    Publish-Control '009-fault-release.json' $arm
                    $faultReleased=$true; $independentBound=$true; $cancelSent=$true
                }
            }
        }
        if ($CaseId -in @('BA05-cancel/pending','BA05-cancel/inflight') -and -not $cancelSent) {
            $faultPath=Join-Path $TestRoot '009-fault-events.jsonl'
            if (Test-Path -LiteralPath $faultPath) {
                $expectedPhase=if ($CaseId -eq 'BA05-cancel/inflight') { 'BindingFirstWriteResponded' } else { 'RecipePreconditionInstalled' }
                $installed=@(Get-Content -LiteralPath $faultPath | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object phase -eq $expectedPhase)
                if ($installed.Count) {
                    $cancelReceipt=Invoke-RestMethod -Method Post "$api/api/v1/station01/runs/$runId/cancel" -Headers $headers -ContentType 'application/json' -Body (
                        @{requestId=[guid]::NewGuid().ToString();expectedRevision=$run.observedRevision;reason='009 controlled cancellation of pending recipe application'} | ConvertTo-Json -Compress)
                    Save-New (Join-Path $root 'cancel-receipt.json') @{atUtc=[datetime]::UtcNow.ToString('o');receipt=$cancelReceipt}
                    $cancelSent=$true
                }
            }
        }
        if (-not $PersistenceFault -and @($run.allowedActions) -contains 'ConfirmManualTrayRemoval') {
            $evidence=Invoke-RestMethod "$api/api/v1/station01/runs/$runId/evidence" -Headers $headers
            if (-not $evidence.wholeTrayCompletionId -or -not @($evidence.stages | Where-Object eventType -eq 'ObservedUnlocked').Count) {
                throw 'ManualRemovalBeforeCommittedUnlock'
            }
            $confirmation=Invoke-RestMethod -Method Post "$api/api/v1/station01/runs/$runId/manual-removal-confirmations" -Headers $headers -ContentType 'application/json' -Body (
                @{requestId=[guid]::NewGuid().ToString();expectedRevision=$run.observedRevision;reason='Test controlled removal after committed unlock'} | ConvertTo-Json -Compress)
            Save-New (Join-Path $root 'manual-removal.json') $confirmation
        }
        if ($run.errorCode -or $run.finalOutcome -ne 0) { break }
    } while ($watch.Elapsed.TotalMinutes -lt 12)
    if ($watch.Elapsed.TotalMinutes -ge 12) { throw 'IndependentCaseHarnessDeadline' }
    if ($PersistenceFault) {
        # Do not release based on elapsed time since StartRun: pickup may occur
        # much later. The actual seam must have been hit; keep its real receipt
        # unavailable beyond the ten-second binding window before releasing it.
        $faultEvents=Join-Path $TestRoot '009-fault-events.jsonl'
        if (-not (Test-Path -LiteralPath $faultEvents)) { throw 'ControlledFaultWasNotHit' }
        $events=@(Get-Content -LiteralPath $faultEvents | ForEach-Object { $_ | ConvertFrom-Json })
        $held=@($events | Where-Object phase -eq 'ReceiptHeld')
        if ($held.Count) {
            $until=([datetime]$held[0].atUtc).ToUniversalTime().AddSeconds(12)
            while ([datetime]::UtcNow -lt $until) { Start-Sleep -Milliseconds 200 }
        }
        if (-not $faultReleased) { Publish-Control '009-fault-release.json' $arm }
        # Observe after the original action is closed and the real store is released.
        Start-Sleep -Seconds 3
        $run=Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $headers
    }
    $phase='collect'
    if ($deviceFaults.ContainsKey($CaseId)) {
        Save-New (Join-Path $root 'fault-run-at-close.json') $run
        # Observe beyond the finite delayed response and original closure. This
        # is an evidence wait, never an extension of any product deadline.
        Start-Sleep -Seconds 3
        $run=Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $headers
    }
    $facts=@{run=$run;runEvidence=(Invoke-RestMethod "$api/api/v1/station01/runs/$runId/evidence" -Headers $headers);
        status=(Invoke-RestMethod "$api/api/v1/station01/status" -Headers $headers);
        plcChanges=(Read-Changes);plcWriteAudit=(Invoke-RestMethod "$plcApi/api/simulator/audit?after=0");plcState=(Invoke-RestMethod "$plcApi/api/simulator/state");
        source='ActualBackendAPI;NoPageClaim';observedUtc=[datetime]::UtcNow.ToString('o')}
    if ($faultRunId) { $facts.faultRun=Invoke-RestMethod "$api/api/v1/station01/runs/$faultRunId" -Headers $engineerHeaders }
    Save-New (Join-Path $root 'process-api-facts.json') $facts
    Save-New (Join-Path $root 'run-latest.json') $run
    if ($CaseId -eq 'F06-C') {
        $phase='restart-reconciliation'
        & python -B (Join-Path $PSScriptRoot 'validate-009-process-business.py') $root --snapshot-recovery-input --store $TestRoot --run-id $runId
        if ($LASTEXITCODE -ne 0) { throw 'ActualRecoveryInputSnapshotFailed' }
        foreach ($item in @($owned | Where-Object key -in @('hostPid','workerPid'))) {
            Stop-OwnedRecorded $item
        }
        # Restart only the actual Host on its existing committed Test database.
        # Keep the independent PLC and its complete action audit alive.
        Remove-Item Env:Gaode__TestPersistenceFaultCase -ErrorAction SilentlyContinue
        $restartOptions=@{}
        if ($WindowsNativeThreadPool) { $restartOptions.Environment=@{DOTNET_ThreadPool_UseWindowsThreadPool='1';DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS='0'} }
        $restarted=Start-Process -FilePath 'dotnet' -ArgumentList ('"'+$HostDll+'" --urls '+$api) -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $TestRoot 'logs/host.restart.out.log') -RedirectStandardError (Join-Path $TestRoot 'logs/host.restart.err.log') @restartOptions
        # Start-Process can return before MainModule/Path is available. Resolve
        # the live process identity before recording the ownership constraint.
        $launched=$restarted
        $identityLimit=[datetime]::UtcNow.AddSeconds(5)
        do {
            if ($launched.HasExited) { throw 'RecoveryHostExitedBeforeIdentity' }
            $restarted=Get-Process -Id $launched.Id -ErrorAction Stop
            if ($restarted.Path) { break }
            Start-Sleep -Milliseconds 100
        } while ([datetime]::UtcNow -lt $identityLimit)
        if (-not $restarted.Path -or $restarted.Path -ne (Get-Command dotnet).Source) {
            # The original Start-Process handle identifies only our launched
            # child, even before its executable module becomes observable.
            if (-not $launched.HasExited) { $launched.Kill(); $null=$launched.WaitForExit(10000) }
            throw 'RecoveryHostExecutableNotObserved'
        }
        $owned.Add(@{key='restartedHostPid';id=$restarted.Id;path=$restarted.Path;start=$restarted.StartTime.ToUniversalTime().Ticks})
        $ready=$false; $restartLimit=[datetime]::UtcNow.AddSeconds(45)
        do {
            if ($restarted.HasExited) { throw 'RecoveryHostExited' }
            try {
                $restartStatus=Invoke-RestMethod "$api/api/v1/station01/status" -Headers $headers
                $ready=$restartStatus.algorithm.state -eq 'Ready'
            } catch { }
            if (-not $ready) { Start-Sleep -Milliseconds 250 }
        } while (-not $ready -and [datetime]::UtcNow -lt $restartLimit)
        if (-not $ready) { throw 'RecoveryHostReadinessDeadline' }
        $workerRecords=@(Get-Content -LiteralPath (Join-Path $TestRoot 'logs/host.restart.out.log') | ForEach-Object {
            $offset=$_.IndexOf('RuntimeFlow {',[StringComparison]::Ordinal)
            if ($offset -ge 0) { $_.Substring($offset+'RuntimeFlow '.Length) | ConvertFrom-Json }
        } | Where-Object { $_.step -eq 'WorkerProcess' -and $_.outcome -eq 'Ready' -and $_.processId -eq $restarted.Id })
        if ($workerRecords.Count -ne 1) { throw 'RecoveryOwnedWorkerIdentityMissing' }
        $restartWorker=Get-Process -Id ([int]$workerRecords[0].facts.processId) -ErrorAction Stop
        if ($restartWorker.ProcessName -notmatch '^python(w)?$' -or $restartWorker.StartTime.ToUniversalTime() -lt $restarted.StartTime.ToUniversalTime()) { throw 'RecoveryWorkerPredatesHost' }
        $owned.Add(@{key='restartedWorkerPid';id=$restartWorker.Id;path=$restartWorker.Path;start=$restartWorker.StartTime.ToUniversalTime().Ticks})
        Save-New (Join-Path $root 'restart-owned-processes.json') @($owned | Where-Object key -like 'restarted*')
        Start-Sleep -Seconds 3
        Save-New (Join-Path $root 'restart-facts.json') @{
            beforeHostPid=$platform.hostPid;afterHostPid=$restarted.Id;afterWorkerPid=$restartWorker.Id;plcPid=$platform.plcPid;
            hostDllSha256=(Get-FileHash -LiteralPath $HostDll -Algorithm SHA256).Hash.ToLowerInvariant();startedUtc=$restarted.StartTime.ToUniversalTime().ToString('o');
            run=(Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $headers);
            runEvidence=(Invoke-RestMethod "$api/api/v1/station01/runs/$runId/evidence" -Headers $headers);
            status=(Invoke-RestMethod "$api/api/v1/station01/status" -Headers $headers);
            actionAudit=(Invoke-RestMethod "$plcApi/api/simulator/audit?after=0");observedUtc=[datetime]::UtcNow.ToString('o');
            physicalActionRequested=$false;origin='ActualHostRestart;SameControlledDatabase;IndependentPlcUnchanged'}
    }
} catch {
    $errorText=$_.Exception.ToString()
    throw
} finally {
    foreach ($item in $owned) {
        try {
            Stop-OwnedRecorded $item
        } catch { $cleanup += $_.Exception.Message }
    }
    Save-New (Join-Path $root 'collection.json') @{caseId=$CaseId;runId=$runId;testRoot=$TestRoot;
        fixture=$FixtureManifest;phase=$phase;error=$errorText;cleanupErrors=$cleanup;
        atUtc=[datetime]::UtcNow.ToString('o');pageVerified=$false;acceptance='NotEvaluated';
        hostDll=$HostDll;plcDll=$PlcDll;windowsNativeThreadPool=[bool]$WindowsNativeThreadPool}
    if ($cleanup.Count) { throw ('OwnedCleanupFailed: '+($cleanup -join ';')) }
}
