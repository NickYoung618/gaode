param([switch]$Interactive, [switch]$PreflightOnly, [Parameter(Mandatory)][string]$EvidenceRoot,
      [string[]]$Cases = @('Q01','Q02'), [string]$HostDll = '', [string]$PlcDll = '',
      [switch]$WindowsNativeThreadPool, [switch]$ExternalTestData, [switch]$HostSocketInlineCompletions, [switch]$PlcSocketInlineCompletions,
      [ValidateSet('','Auth401','Auth403')][string]$AuthorizationMode = '')
$ErrorActionPreference = 'Stop'
$PSDefaultParameterValues['Invoke-RestMethod:TimeoutSec'] = 10
$Cases = @($Cases | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
if ($AuthorizationMode -and ($PreflightOnly -or $Cases.Count -ne 1 -or $Cases[0] -cne 'Q01')) {
    throw 'Authorization page verification requires exactly Q01 and no preflight.'
}
$supported = @('Q01','Q02','Q01-PARAM','Q03','Q03-NG','Q03-Pending','GROUP-F','GROUP-F-MIXED','GROUP-F-PENDING','GROUP-A-E',
    'ASSEMBLY-A-E','ASSEMBLY-A-E-NOCODE','ASSEMBLY-A-E-ERROR','ASSEMBLY-A-E-NG','Q04-MANUAL',
    'ROT-PART-OK','ROT-PART-NG','ROT-PART-PENDING','ROT-ASSEMBLY-OK','ROT-ASSEMBLY-NG','ROT-ASSEMBLY-PENDING',
    'RECOVERY-3D','RECOVERY-F','Q01-PAUSE','Q01-NG','Q02-PENDING','Q02-PENDING-P03','ASSEMBLY-A-E-MANUAL','ASSEMBLY-A-E-PENDING') +
    @(4..22 | ForEach-Object { 'Q{0:d2}' -f $_ })
foreach ($case in $Cases) {
    if ($case -cnotin $supported) { throw "Unsupported case: $case" }
}
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/recipe-execution-008'))
$root = [IO.Path]::GetFullPath($EvidenceRoot)
$directInteractive = -not $Interactive -and (Get-Process -Id $PID).SessionId -ne 0
if (-not $root.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)) { throw 'EvidenceRoot outside isolated 008 artifacts.' }
if (-not $Interactive) {
    if (Test-Path -LiteralPath $root) {
        if (-not $directInteractive -or
            @(Get-ChildItem -LiteralPath $root -Force).Count -ne 0) {
            throw 'EvidenceRoot must be new or empty for a direct interactive run.'
        }
    } else { New-Item -ItemType Directory -Path $root -ErrorAction Stop | Out-Null }
} elseif (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw 'Interactive worker requires the dispatch evidence root.'
}

function Save-Json([string]$path, $value) {
    $value | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $path -Encoding utf8
}
function Get-CompletePlcChanges([string]$apiBase) {
    $first = Invoke-RestMethod "$apiBase/api/simulator/changes?after=0"
    $target = [long]$first.latestSequence
    $cursor = 0L
    $all = [Collections.Generic.List[object]]::new()
    $page = $first
    $pages = 0
    do {
        $pages++
        $before = $cursor
        if ($page.gap) { throw "PLC changes gap after $cursor; incomplete evidence." }
        foreach ($change in @($page.changes)) {
            if ([long]$change.sequence -gt $target) { break }
            if ([long]$change.sequence -ne $cursor + 1) {
                throw "PLC changes cursor discontinuity after $cursor."
            }
            $all.Add($change); $cursor = [long]$change.sequence
        }
        if ($cursor -ge $target) { break }
        if ($cursor -eq $before) { throw "PLC changes cursor stalled after $cursor." }
        $page = Invoke-RestMethod "$apiBase/api/simulator/changes?after=$cursor"
    } while ($cursor -lt $target)
    return @{ oldestSequence = $first.oldestSequence; latestSequence = $target; gap = $false
        changes = $all.ToArray(); pageCount = $pages; completeThroughFrozenSequence = $cursor }
}
if (-not $Interactive -and -not $directInteractive) {
    if ($HostDll -or $PlcDll -or $WindowsNativeThreadPool -or $ExternalTestData -or $HostSocketInlineCompletions -or $PlcSocketInlineCompletions -or $AuthorizationMode) { throw 'Frozen-build WPF verification must use the current interactive worker.' }
    $taskName = 'GaodeQ01Q02-' + [guid]::NewGuid().ToString('N')
    $scheduler = New-Object -ComObject Schedule.Service
    $scheduler.Connect()
    $folder = $scheduler.GetFolder('\')
    $definition = $scheduler.NewTask(0)
    $definition.Principal.UserId = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $definition.Principal.LogonType = 3 # same-user InteractiveToken
    $definition.Principal.RunLevel = 0 # LUA
    $definition.Settings.ExecutionTimeLimit = 'PT18M'
    $definition.Settings.Hidden = $true
    $definition.Settings.DisallowStartIfOnBatteries = $false
    $action = $definition.Actions.Create(0)
    $action.Path = (Get-Command pwsh).Source
    $action.Arguments = '-NoProfile -NonInteractive -WindowStyle Hidden -File "' + $PSCommandPath +
        '" -Interactive -EvidenceRoot "' + $root + '" -Cases ' + ($Cases -join ',')
    $action.WorkingDirectory = $repo
    $task = $null
    try {
        $task = $folder.RegisterTaskDefinition($taskName, $definition, 2, $null, $null, 3, $null)
        $instance = $task.Run($null)
        Save-Json (Join-Path $root 'dispatch.json') @{
            task = $taskName; instance = $instance.InstanceGuid; requestedAtUtc = [datetime]::UtcNow.ToString('o')
            user = $definition.Principal.UserId; logonType = 'InteractiveToken'; runLevel = 'LUA'
        }
        $deadline = (Get-Date).AddMinutes(18)
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 2
            if (Test-Path (Join-Path $root 'validation-result.json')) {
                $result = Get-Content (Join-Path $root 'validation-result.json') -Raw | ConvertFrom-Json
                Write-Output (Join-Path $root 'validation-result.json')
                if ($result.exitCode -ne 0) { throw "Interactive Q run stopped: $($result.phase)" }
                return
            }
            if ((Get-Date) -gt $deadline.AddMinutes(-17.5) -and
                -not (Test-Path (Join-Path $root 'interactive-start.json'))) {
                throw 'Same-user interactive session did not start worker.'
            }
        }
        throw 'Interactive Q evidence deadline exceeded.'
    } finally {
        if ($task) {
            $task.Enabled = $false
            try { $task.Stop(0) } catch { }
            $folder.DeleteTask($taskName, 0)
        }
    }
}

if ((Get-Process -Id $PID).SessionId -eq 0) { throw 'WPF run requires interactive session.' }
Save-Json (Join-Path $root 'interactive-start.json') @{
    sessionId = (Get-Process -Id $PID).SessionId; startedAtUtc = [datetime]::UtcNow.ToString('o')
    user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
}
if (-not $HostDll) { $HostDll = Join-Path $repo 'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll' }
if (-not $PlcDll) { $PlcDll = Join-Path $repo 'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll' }
foreach ($binary in @(@{path=$HostDll;name='Gaode.Host.dll'}, @{path=$PlcDll;name='VirtualPlc.dll'})) {
    $full = [IO.Path]::GetFullPath($binary.path)
    if (-not $full.StartsWith($repo + '\',[StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($full) -ne $binary.name) { throw 'Only local project Test Host/PLC binaries are accepted.' }
}
$desktopExe = Join-Path $repo 'desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe'
$dist = Join-Path $repo 'frontend/dist'
$owned = [System.Collections.Generic.List[object]]::new()
$phase = 'preflight'; $exitCode = 1
function Stop-Owned([int]$id, [string]$expected) {
    $process = Get-Process -Id $id -ErrorAction SilentlyContinue
    if (-not $process) { return }
    $known = @($owned | Where-Object id -eq $id)
    if ($known.Count -ne 1 -or $process.StartTime.ToUniversalTime() -ne $known[0].created) { throw "Owned PID $id creation identity changed; preserve it." }
    if (-not [string]::Equals([IO.Path]::GetFullPath([string]$process.Path),
        [IO.Path]::GetFullPath($expected), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Owned PID $id changed identity; refusing to stop it."
    }
    try { $process.Kill() } catch { if (-not $process.HasExited) { throw } }
    if (-not $process.WaitForExit(5000)) { throw "Owned PID $id did not exit." }
}
try {
    foreach ($file in @($hostDll, $plcDll, $desktopExe, (Join-Path $dist 'runtime.js'))) {
        if (-not (Test-Path $file -PathType Leaf)) { throw "Current build missing: $file" }
    }
    $index = 0
    foreach ($case in $Cases) {
        $phase = "$case-preflight"
        $index++
        $caseRoot = Join-Path $root $case
        $dataRoot = $caseRoot
        if ($ExternalTestData) {
            # A new controlled Test store on the interactive account's temporary
            # volume; existing source/evidence stays in place and budgets do not change.
            $env:GAODE_VERIFY_TEST_PARENT=Join-Path ([IO.Path]::GetTempPath()) 'gaode-009-page'
            $dataRoot=Join-Path $env:GAODE_VERIFY_TEST_PARENT ([guid]::NewGuid().ToString('N'))
            $null=New-Item -ItemType Directory -Path $caseRoot
        }
        $ports = if ($case -eq 'Q01') { @(25132,25133,25134,25135) }
                 elseif ($case -eq 'Q02') { @(25142,25143,25144,25145) }
                 elseif ($case -eq 'Q03' -or $case -match '^Q(0[4-9]|1[0-9]|2[0-2])$') { @(25162,25163,25164,25165) }
                 elseif ($case -eq 'Q03-NG') { @(25182,25183,25184,25185) }
                 elseif ($case -eq 'Q03-Pending') { @(25192,25193,25194,25195) }
                 else { @(25152,25153,25154,25155) }
        foreach ($port in $ports) {
            if (@(netstat -ano -p TCP | Select-String "^\s*TCP\s+\S+:$port\s+\S+\s+LISTENING\s+").Count -gt 0) {
                throw "Isolated port in use: $port"
            }
        }
        $fixtureName = if ($case -eq 'Q01') { 'fixture.json' }
                       elseif ($case -eq 'Q02') { 'fixture-q02.json' }
                       elseif ($case -eq 'Q03') { 'fixture-q03.json' }
                       elseif ($case -eq 'Q03-NG') { 'fixture-q03-ng.json' }
                       elseif ($case -eq 'Q03-Pending') { 'fixture-q03-pending.json' }
                       elseif ($case -eq 'GROUP-F') { 'fixture-group-f.json' }
                       elseif ($case -eq 'GROUP-F-MIXED') { 'fixture-group-f-mixed.json' }
                       elseif ($case -eq 'GROUP-A-E') { 'fixture-group-a-e.json' }
                       elseif ($case -eq 'Q04-MANUAL') { 'fixture-q04-manual.json' }
                       elseif ($case -eq 'ASSEMBLY-A-E-MANUAL') { 'fixture-assembly-a-e-manual.json' }
                       elseif ($case -eq 'RECOVERY-3D') { 'fixture-recovery-3d.json' }
                       elseif ($case -match '^ROT-(PART|ASSEMBLY)-(OK|NG|PENDING)$') { "fixture-$($case.ToLowerInvariant()).json" }
                       elseif ($case -eq 'ASSEMBLY-A-E') { 'fixture-assembly-a-e.json' }
                       elseif ($case -eq 'ASSEMBLY-A-E-NOCODE') { 'fixture-assembly-a-e-no-code.json' }
                       elseif ($case -eq 'ASSEMBLY-A-E-ERROR') { 'fixture-assembly-a-e-error.json' }
                       elseif ($case -eq 'ASSEMBLY-A-E-NG') { 'fixture-assembly-a-e-ng.json' }
                       elseif ($case -match '^Q(0[4-9]|1[0-9]|2[0-2])$') { "fixture-$($case.ToLowerInvariant()).json" }
                       else { 'fixture-q01-param.json' }
        $pageBudget = & (Join-Path $PSScriptRoot 'get-008-page-budget.ps1') -CaseId $case -BinaryRoot (Split-Path $HostDll)
        $fixture = $pageBudget.fixture
        if ($case -eq 'GROUP-A-E') { $fixture = Join-Path $repo "specs/008-recipe-driven-inspection/fixtures/usr-e-1.0.2/$fixtureName" }
        if ($case -match '^Q(0[4-9]|1[0-9]|2[0-2])$') {
            $fixture = Join-Path $repo "specs/008-recipe-driven-inspection/fixtures/usr-e-1.0.2/$fixtureName"
            if ($case -in @('Q07','Q10','Q12','Q13','Q16','Q17','Q19','Q22')) { throw 'Recipe exited current business scope; historical evidence retained.' }
        }
        $api = "http://127.0.0.1:$($ports[0])"; $plcApi = "http://127.0.0.1:$($ports[1])"
        $operatorToken = [guid]::NewGuid().ToString('N')
        $readOnlyToken = if ($AuthorizationMode -eq 'Auth403') { [guid]::NewGuid().ToString('N') } else { '' }
        if ($readOnlyToken) { $env:GAODE_TEST_READ_ONLY_TOKEN = $readOnlyToken }
        $pageAdminToken = if ($case -in @('Q04-MANUAL','ASSEMBLY-A-E-MANUAL','RECOVERY-3D','RECOVERY-F','Q01-PAUSE')) { [guid]::NewGuid().ToString('N') } else { '' }
        $env:GAODE_TEST_OPERATOR_TOKEN = $operatorToken
        $env:Simulation__ScanPeriodMs = '20'
        $phase = "$case-platform"
        $null = & (Join-Path $repo 'scripts/start-station01-virtual-loop.ps1') `
            -OperatorToken $operatorToken -TestPageAdministratorToken $pageAdminToken -TestPageReadOnlyToken $readOnlyToken -TestRoot $dataRoot -ApiBase $api -PlcApiBase $plcApi `
            -PlcPort $ports[2] -HostDll $hostDll -PlcDll $plcDll -FixtureManifest $fixture -SkipDesktop `
            -HostSocketInlineCompletions:$HostSocketInlineCompletions -PlcSocketInlineCompletions:$PlcSocketInlineCompletions -WindowsNativeThreadPool:$WindowsNativeThreadPool
        $platform = Get-Content (Join-Path $dataRoot 'process.json') -Raw | ConvertFrom-Json
        if ($ExternalTestData) {
            Save-Json (Join-Path $caseRoot 'storage-location.json') @{testRoot=$dataRoot;origin='ActualControlledTestStore';processRecord=(Join-Path $dataRoot 'process.json')}
            Copy-Item -LiteralPath (Join-Path $dataRoot 'process.json') -Destination (Join-Path $caseRoot 'process.json')
        }
        $owned.Add([pscustomobject]@{ id = [int]$platform.hostPid; created = (Get-Process -Id ([int]$platform.hostPid)).StartTime.ToUniversalTime(); expected = (Get-Command dotnet).Source })
        $owned.Add([pscustomobject]@{ id = [int]$platform.plcPid; created = (Get-Process -Id ([int]$platform.plcPid)).StartTime.ToUniversalTime(); expected = (Get-Command dotnet).Source })
        # Only these newly created Test processes get scheduling preference. The
        # protocol's 1-second I/O and 3-second heartbeat limits are unchanged.
        $hostProcess = Get-Process -Id ([int]$platform.hostPid) -ErrorAction Stop
        $plcProcess = Get-Process -Id ([int]$platform.plcPid) -ErrorAction Stop
        $hostProcess.PriorityClass = [Diagnostics.ProcessPriorityClass]::High
        $plcProcess.PriorityClass = [Diagnostics.ProcessPriorityClass]::High
        Save-Json (Join-Path $caseRoot 'scheduling.json') @{
            source = 'Test/isolated-process-scheduling'; hostPid = $hostProcess.Id
            plcPid = $plcProcess.Id; hostPriority = $hostProcess.PriorityClass.ToString()
            plcPriority = $plcProcess.PriorityClass.ToString()
            ioTimeoutMs = 1000; heartbeatTimeoutMs = 3000
            hostAcquisition = "plc-acquisition/013-1"; virtualPlcScanMs = 20
        }
        $phase = "$case-prepare"
        $prepared = [string](& (Join-Path $repo 'scripts/simulate-station01-load.ps1') `
            -PrepareOnly -OutputDirectory $caseRoot -ApiBase $api -FixtureManifest $fixture)
        if (-not (Test-Path $prepared -PathType Leaf)) { throw 'PrepareOnly output missing.' }
        $env:GAODE_TEST_PREPARED_LOAD_PATH = [IO.Path]::GetFullPath($prepared)
        if ($pageAdminToken) { $env:GAODE_TEST_OPERATOR_TOKEN = $pageAdminToken }
        Save-Json (Join-Path $caseRoot 'page-case.json') @{
            caseId = $case; fixture = $fixture; collectorWaitMs = if ($AuthorizationMode) { 30000 } else { $pageBudget.collectorWaitMs }
            authorizationMode = $AuthorizationMode
            budget = $pageBudget
            pageRole = if ($pageAdminToken) { 'Test/SystemAdministrator' } else { 'Test/Operator' }
            manualOccupancyInjection = $case -in @('Q04-MANUAL','ASSEMBLY-A-E-MANUAL'); initialMoveTimeoutInjection = $case -eq 'RECOVERY-3D'
            afterThreeDMoveTimeoutInjection = $case -eq 'RECOVERY-F'; normalPauseInjection = $case -eq 'Q01-PAUSE'
            businessTimeoutsChanged = $false
        }
        if ($case -eq 'RECOVERY-3D') { $null = Invoke-RestMethod "$plcApi/api/simulator/faults/MoveTimeout" -Method Post }
        $env:GAODE_MODE = 'Test'; $env:GAODE_API_BASE_URL = $api
        $env:GAODE_SIGNALR_URL = "$api/hubs/station01"
        $env:GAODE_FRONTEND_DIST = $dist
        $env:WEBVIEW2_USER_DATA_FOLDER = Join-Path $dataRoot 'webview2-profile'
        $env:GAODE_TEST_WEBVIEW2_DEBUG_PORT = [string]$ports[3]
        $phase = "$case-page"
        $desktop = Start-Process -FilePath $desktopExe -WorkingDirectory (Split-Path $desktopExe) `
            -WindowStyle Normal -PassThru
        $owned.Add([pscustomobject]@{ id = $desktop.Id; created = $desktop.StartTime.ToUniversalTime(); expected = $desktopExe })
        Save-Json (Join-Path $caseRoot 'interactive-desktop.json') @{
            pid = $desktop.Id; sessionId = (Get-Process -Id $desktop.Id).SessionId
            exeSha256 = (Get-FileHash $desktopExe -Algorithm SHA256).Hash
            runtimeSha256 = (Get-FileHash (Join-Path $dist 'runtime.js') -Algorithm SHA256).Hash
            preparedSha256 = (Get-FileHash $prepared -Algorithm SHA256).Hash
            debugPort = $ports[3]; source = 'Test/actual-WPF-WebView2'
        }
        $debugReady = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            Start-Sleep -Milliseconds 500
            if ($desktop.HasExited) { throw "$case WPF exited before WebView2 was available." }
            try {
                $targets = @(Invoke-RestMethod "http://127.0.0.1:$($ports[3])/json/list" -TimeoutSec 2)
                if (@($targets | Where-Object { $_.type -eq 'page' -and
                    $_.url -like 'https://appassets.local/*' }).Count -gt 0) {
                    $debugReady = $true; break
                }
            } catch { }
        }
        if (-not $debugReady) { throw "$case actual WebView2 page did not expose controlled CDP." }
        $recipeId = (Get-Content -LiteralPath $fixture -Raw | ConvertFrom-Json).recipeRef.recipeId
        $captureMode = if ($PreflightOnly) { 'Preflight' } elseif ($AuthorizationMode) { $AuthorizationMode } else { 'Recipe' }
        & node (Join-Path $repo 'scripts/capture-station01-webview2-normal.cjs') `
            $ports[3] $caseRoot $captureMode $recipeId `
            1> (Join-Path $caseRoot 'page-capture.out.log') `
            2> (Join-Path $caseRoot 'page-capture.err.log')
        $pageExit = $LASTEXITCODE
        $page = Get-Content (Join-Path $caseRoot "$($captureMode.ToLowerInvariant())-webview2-page-evidence.json") -Raw | ConvertFrom-Json
        $phase = "$case-facts"
        if ($AuthorizationMode) {
            Save-Json (Join-Path $caseRoot 'authorization-api-device-facts.json') @{
                status = Invoke-RestMethod "$api/api/v1/station01/status" -Headers @{Authorization="Bearer $operatorToken"}
                plcWriteAudit = Invoke-RestMethod "$plcApi/api/simulator/audit"
                source = 'Test/real authorization;VirtualPlc'; authorizationMode = $AuthorizationMode
            }
        }
        if ($page.receipt.body.runId) {
            $runId = [string]$page.receipt.body.runId
            $commandId = [string]$page.receipt.body.commandId
            $headers = @{ Authorization = "Bearer $operatorToken" }
            $facts = [ordered]@{
                source = 'Test/WPF-WebView2;VirtualPlc;Worker;SQLite'; realDeviceVerified = $false
                caseId = $case; runId = $runId; commandId = $commandId
                status = Invoke-RestMethod "$api/api/v1/station01/status" -Headers $headers
                run = Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $headers
                evidence = Invoke-RestMethod "$api/api/v1/station01/runs/$runId/evidence" -Headers $headers
                command = Invoke-RestMethod "$api/api/v1/station01/commands/$commandId" -Headers $headers
                media = Invoke-RestMethod "$api/api/v1/station01/runs/$runId/media" -Headers $headers
                plcState = Invoke-RestMethod "$plcApi/api/simulator/state"
                plcChanges = Get-CompletePlcChanges $plcApi
                plcWriteAudit = Invoke-RestMethod "$plcApi/api/simulator/audit"
            }
            if ($page.faultReceipt.body.runId) {
                $faultRunId = [string]$page.faultReceipt.body.runId
                $facts.faultRun = Invoke-RestMethod "$api/api/v1/station01/runs/$faultRunId" -Headers $headers
                $facts.faultEvidence = Invoke-RestMethod "$api/api/v1/station01/runs/$faultRunId/evidence" -Headers $headers
                $facts.faultMedia = Invoke-RestMethod "$api/api/v1/station01/runs/$faultRunId/media" -Headers $headers
            }
            if ($case -like 'ROT-*') {
            }
            Save-Json (Join-Path $caseRoot 'page-api-device-facts.json') $facts
        }
        Save-Json (Join-Path $caseRoot 'case-result.json') @{
            caseId = $case; pageExit = $pageExit; pageOutcome = $page.outcome
            runId = $page.receipt.body.runId; atUtc = [datetime]::UtcNow.ToString('o')
        }
        Stop-Owned $desktop.Id $desktopExe
        Stop-Owned ([int]$platform.hostPid) (Get-Command dotnet).Source
        Stop-Owned ([int]$platform.plcPid) (Get-Command dotnet).Source
        $owned.Clear()
        if ($AuthorizationMode) {
            if ($pageExit -ne 0) { throw 'Authorization WPF collector failed; preserve original evidence.' }
            & python (Join-Path $repo 'scripts/validate-008-authorization-page.py') $caseRoot $AuthorizationMode
            if ($LASTEXITCODE -ne 0) { throw 'Authorization actual page/real denial/no execution checks failed.' }
            & python (Join-Path $repo 'scripts/communication/check-009-route-wire.py') authorization $caseRoot
            if ($LASTEXITCODE -ne 0) { throw 'Authorization communication evidence failed.' }
            continue
        }
        if ($PreflightOnly) {
            if ($pageExit -ne 0 -or $page.outcome -ne 'ConnectedAndExited') { throw 'Short actual WPF collector preflight failed.' }
            continue
        }
        if ($pageExit -ne 0 -or $page.outcome -ne 'FinalPageDisplayed') {
            throw "$case WPF page did not reach Final: $($page.outcome); no automatic retry."
        }
        $phase = "$case-persisted-route-readback"
        & python (Join-Path $repo 'scripts/validate-008-operation-evidence.py') $caseRoot $fixture
        if ($LASTEXITCODE -ne 0) { throw "$case persisted route readback failed; page Final alone is insufficient." }
        & python (Join-Path $repo 'scripts/communication/check-009-operation-wire.py') $caseRoot $fixture
        if ($LASTEXITCODE -ne 0) { throw "$case independent communication evidence failed." }
    }
    $exitCode = 0
} catch {
    Save-Json (Join-Path $root 'validation-error.json') @{
        atUtc = [datetime]::UtcNow.ToString('o'); phase = $phase
        error = $_.Exception.ToString(); noAutomaticRetry = $true
    }
} finally {
    $cleanupErrors = @()
    for ($i = $owned.Count - 1; $i -ge 0; $i--) {
        try { Stop-Owned $owned[$i].id $owned[$i].expected } catch { $cleanupErrors += $_.Exception.Message }
    }
    if ($cleanupErrors.Count -gt 0) { $exitCode = 1 }
    Save-Json (Join-Path $root 'cleanup-result.json') @{ errors = $cleanupErrors; verified = $cleanupErrors.Count -eq 0 }
    Remove-Item Env:GAODE_TEST_OPERATOR_TOKEN -ErrorAction SilentlyContinue
    Remove-Item Env:GAODE_TEST_READ_ONLY_TOKEN -ErrorAction SilentlyContinue
    Remove-Item Env:Gaode__Tokens__EquipmentEngineer -ErrorAction SilentlyContinue
    Remove-Item Env:Gaode__Tokens__SystemAdministrator -ErrorAction SilentlyContinue
    Save-Json (Join-Path $root 'validation-result.json') @{
        atUtc = [datetime]::UtcNow.ToString('o'); phase = $phase
        exitCode = $exitCode; sessionId = (Get-Process -Id $PID).SessionId
        source = 'Test/VirtualPlc only'; realDeviceVerified = $false
    }
}
exit $exitCode

