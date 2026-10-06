param(
    [string]$OperatorToken = $env:GAODE_TEST_OPERATOR_TOKEN,
    [string]$TestPageAdministratorToken = '',
    [string]$TestPageReadOnlyToken = '',
    [string]$TestRoot = '',
    [string]$ApiBase = 'http://127.0.0.1:5001',
    [string]$PlcApiBase = 'http://127.0.0.1:5080',
    [int]$PlcPort = 1502,
    [string]$HostDll = '',
    [string]$PlcDll = '',
    [string]$PageOrigin = 'https://appassets.local',
    [string]$FixtureManifest = '',
    [ValidateSet('', 'F05-A', 'F05-B', 'F05-C', 'F06-A', 'F06-B', 'F06-C', 'BA04-late-bound', 'BA04-late-handoff', 'BA06-before-port', 'BA06-during-save', 'BA06-before-next', 'BA02-api-input')]
    [string]$TestPersistenceFaultCase = '',
    [switch]$HostSocketInlineCompletions,
    [switch]$PlcSocketInlineCompletions,
    [switch]$SkipDesktop,
    [switch]$WindowsNativeThreadPool,
    [switch]$DiagnosticEarlyReady
)
$ErrorActionPreference = 'Stop'
function Resolve-TestStorageRoot {
    param([string]$Root, [string]$Repository, [bool]$HasFixture, [string]$ExternalParent)
    $allowedRoot = [IO.Path]::GetFullPath((Join-Path $Repository $(if ($HasFixture) { 'artifacts/recipe-execution-008' } else { 'artifacts/station01-007' })))
    if ($ExternalParent) {
        if (-not $HasFixture -or -not [IO.Path]::IsPathFullyQualified($Root) -or
            -not [IO.Path]::IsPathFullyQualified($ExternalParent)) { throw 'ExternalTestRootRequiresAbsolutePathsAndTestFixture' }
        $allowedRoot = [IO.Path]::GetFullPath($ExternalParent).TrimEnd([IO.Path]::DirectorySeparatorChar)
        $temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
        if (-not $allowedRoot.StartsWith($temporary + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'ExternalTestParentOutsideUserTemporaryDirectory'
        }
    }
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not $fullRoot.StartsWith($allowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "TestRoot must be below $allowedRoot."
    }
    if ($ExternalParent) {
        if (Test-Path -LiteralPath $fullRoot) { throw 'ExternalTestRootMustBeNew' }
        for ($ancestor = [IO.DirectoryInfo]::new($fullRoot); $null -ne $ancestor; $ancestor = $ancestor.Parent) {
            if ($ancestor.Exists -and ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw 'ExternalTestRootAncestorCannotBeLink'
            }
        }
    }
    return @{ Root = $fullRoot; Allowed = $allowedRoot }
}
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$feature = Join-Path $repo 'specs/007-station01-integrated-loop'
$fixture = $null
if ($TestPersistenceFaultCase -and -not $FixtureManifest) { throw 'Persistence fault requires a validated Test fixture.' }
if ($WindowsNativeThreadPool -and (-not $FixtureManifest -or -not $IsWindows)) { throw 'WindowsNativeThreadPool requires a Windows Test fixture.' }
if ($WindowsNativeThreadPool -and ($HostSocketInlineCompletions -or $PlcSocketInlineCompletions)) { throw 'Native threadpool verification requires inline=0 on both processes.' }
if ($TestPageReadOnlyToken -and -not $FixtureManifest) { throw 'Read-only page credential requires a validated Test fixture.' }
if ($HostSocketInlineCompletions -and -not $FixtureManifest) {
    throw 'HostSocketInlineCompletions requires a validated Test fixture.'
}
if ($PlcSocketInlineCompletions -and -not $FixtureManifest) {
    throw 'PlcSocketInlineCompletions requires a validated Test fixture.'
}
if ($FixtureManifest) {
    if (-not [IO.Path]::IsPathFullyQualified($FixtureManifest)) { throw 'FixtureManifest must be absolute.' }
    $FixtureManifest = [IO.Path]::GetFullPath($FixtureManifest)
    $fixture = Get-Content -LiteralPath $FixtureManifest -Raw | ConvertFrom-Json -AsHashtable
    if ($fixture.purpose -ne 'Test' -or -not $fixture.caseId -or
        -not $fixture.recipeRef.recipeId -or -not $fixture.recipeRef.version -or
        -not $fixture.scenarioId -or @($fixture.occupiedSlots).Count -lt 1) {
        throw 'FixtureManifest lacks Test purpose, case, recipe or occupied slots.'
    }
    foreach ($key in @('configRoot','recipeCatalogPath','imageManifestPath','workerManifestPath','workerScriptPath')) {
        $value = [string]$fixture[$key]
        if (-not [IO.Path]::IsPathFullyQualified($value) -or -not (Test-Path -LiteralPath $value)) {
            throw "FixtureManifest $key must identify an existing absolute path."
        }
        $fixture[$key] = [IO.Path]::GetFullPath($value)
    }
    foreach ($key in @('publicConfigRef','budgetRef','simulationRef')) {
        if (-not $fixture[$key].id -or -not $fixture[$key].version) {
            throw "FixtureManifest $key needs id and version."
        }
    }
    $catalogDigest = (Get-FileHash -LiteralPath $fixture.recipeCatalogPath -Algorithm SHA256).Hash
    if ($catalogDigest -ne [string]$fixture.recipeRef.catalogDigest) {
        throw 'FixtureManifest recipe catalog digest differs from recipeRef.'
    }
    $catalogJson = Get-Content -LiteralPath $fixture.recipeCatalogPath -Raw | ConvertFrom-Json
    $selected = @($catalogJson.recipes | Where-Object {
        $_.recipeId -ceq $fixture.recipeRef.recipeId -and
        $_.version -ceq $fixture.recipeRef.version -and
        $_.manuallySelectedScenario -ceq $fixture.scenarioId -and
        $_.fCode.virtualExactPayload -ceq $fixture.fCode
    })
    if ($selected.Count -ne 1) { throw 'FixtureManifest recipe, scenario or F code does not identify one catalog entry.' }
    $workerJson = Get-Content -LiteralPath $fixture.workerManifestPath -Raw | ConvertFrom-Json
    if ($workerJson.purpose -ne 'Test' -or $workerJson.fCode -cne $fixture.fCode) {
        throw 'FixtureManifest worker Test F code does not match the recipe.'
    }
    $mediaJson = Get-Content -LiteralPath $fixture.imageManifestPath -Raw | ConvertFrom-Json
    if ($mediaJson.purpose -ne 'Test') { throw 'FixtureManifest media purpose must be Test.' }
    foreach ($media in $mediaJson.images) {
        $mediaPath = [IO.Path]::GetFullPath((Join-Path (Split-Path $fixture.imageManifestPath) $media.relativePath))
        if (-not (Test-Path -LiteralPath $mediaPath) -or
            (Get-FileHash -LiteralPath $mediaPath -Algorithm SHA256).Hash -ne $media.sha256) {
            throw "FixtureManifest media missing or digest differs: $($media.role)"
        }
    }
}
if (-not $OperatorToken) { throw 'Set GAODE_TEST_OPERATOR_TOKEN to a controlled local Test token.' }
if (-not $TestRoot) {
    $relative = if ($fixture) { 'artifacts/recipe-execution-008' } else { 'artifacts/station01-007' }
    $TestRoot = Join-Path $repo ($relative + '/runtime-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$storageLocation = Resolve-TestStorageRoot -Root $TestRoot -Repository $repo -HasFixture ($null -ne $fixture) -ExternalParent $env:GAODE_VERIFY_TEST_PARENT
$TestRoot = $storageLocation.Root
$allowed = $storageLocation.Allowed
if (-not (Test-Path (Join-Path $TestRoot 'station01.test.db'))) {
    if ((Test-Path $TestRoot) -and @(Get-ChildItem -LiteralPath $TestRoot -Force).Count -gt 0) {
        throw 'Existing nonempty TestRoot has no prepared SQLite store; choose an empty root.'
    }
    # Use the already built StorePrep; a formal job must not rebuild shared binaries.
    dotnet run --no-build --project (Join-Path $repo 'backend/tools/Gaode.StorePrep/Gaode.StorePrep.csproj') -- $allowed $TestRoot
    if ($LASTEXITCODE -ne 0) { throw 'Test SQLite preparation failed.' }
}
New-Item -ItemType Directory -Path (Join-Path $TestRoot 'logs') -Force | Out-Null
$python = (Get-Command python -ErrorAction Stop).Source
$workerScript = Join-Path $repo 'scripts/virtual-station01-algorithm.py'
$algorithmManifest = Join-Path $feature 'examples/virtual-algorithm.json'
$imageManifest = Join-Path $feature 'examples/images.json'
if ($fixture) {
    $workerScript = $fixture.workerScriptPath
    $algorithmManifest = $fixture.workerManifestPath
    $imageManifest = $fixture.imageManifestPath
}
if (-not $HostDll) { $HostDll = Join-Path $repo 'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll' }
if (-not $PlcDll) { $PlcDll = Join-Path $repo 'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll' }
$hostDll = [IO.Path]::GetFullPath($HostDll)
$plcDll = [IO.Path]::GetFullPath($PlcDll)
if (-not (Test-Path $hostDll) -or -not (Test-Path $plcDll)) {
    throw 'Build Host and VirtualPlc first with dotnet build.'
}
$env:ASPNETCORE_ENVIRONMENT = 'VirtualPlc'
$env:Gaode__Tokens__Operator = $OperatorToken
if ($TestPageReadOnlyToken) { $env:Gaode__Tokens__EquipmentEngineer = $TestPageReadOnlyToken }
else { Remove-Item Env:Gaode__Tokens__EquipmentEngineer -ErrorAction SilentlyContinue }
if ($TestPageAdministratorToken) { $env:Gaode__Tokens__SystemAdministrator = $TestPageAdministratorToken }
else { Remove-Item Env:Gaode__Tokens__SystemAdministrator -ErrorAction SilentlyContinue }
$env:Gaode__Mode = 'VirtualPlcIntegration'
if ($TestPersistenceFaultCase) { $env:Gaode__TestPersistenceFaultCase = $TestPersistenceFaultCase }
else { Remove-Item Env:Gaode__TestPersistenceFaultCase -ErrorAction SilentlyContinue }
$env:Gaode__TestRoot = $TestRoot
$env:Gaode__AllowedTestRoot = $allowed
$env:Gaode__ConfigRoot = if ($fixture) { $fixture.configRoot } else { Join-Path $feature 'examples' }
$env:Gaode__SchemaRoot = Join-Path $repo 'specs/001-station01-public-preparation/contracts'
if ($fixture) {
    $env:Gaode__PublicId = $fixture.publicConfigRef.id
    $env:Gaode__PublicVersion = $fixture.publicConfigRef.version
    $env:Gaode__BudgetId = $fixture.budgetRef.id
    $env:Gaode__BudgetVersion = $fixture.budgetRef.version
    $env:Gaode__SimulationId = $fixture.simulationRef.id
    $env:Gaode__SimulationVersion = $fixture.simulationRef.version
} else {
    $env:Gaode__PublicId = 's01-public-virtual-loop'
    $env:Gaode__PublicVersion = '1.2.0'
    $env:Gaode__BudgetId = 's01-budget-virtual-loop'
    $env:Gaode__BudgetVersion = '3.0.0'
    $env:Gaode__SimulationId = 's01-sim-virtual-loop'
    $env:Gaode__SimulationVersion = '3.0.0'
}
$env:Gaode__ImageManifestPath = $imageManifest
$env:Gaode__WorkerExecutablePath = $python
$env:Gaode__WorkerScriptPath = $workerScript
$env:Gaode__WorkerManifestPath = $algorithmManifest
$env:Gaode__TestAllowedOrigin = $PageOrigin
$env:Gaode__PlcIoTimeoutMs = '1000'
$env:Gaode__PlcPort = [string]$PlcPort
$env:Modbus__Port = [string]$PlcPort
$env:Dashboard__OpenBrowserOnStart = 'false'
$env:GAODE_TEST_OPERATOR_TOKEN = $OperatorToken
$env:GAODE_API_BASE_URL = $ApiBase
$env:GAODE_SIGNALR_URL = "$ApiBase/hubs/station01"
$env:GAODE_MODE = 'Test'
$env:Simulation__HeartbeatTimeoutMs = '3000'
$record = [ordered]@{ startedAtUtc = [datetime]::UtcNow.ToString('o'); testRoot = $TestRoot; apiBase = $ApiBase; plcApiBase = $PlcApiBase; plcPort = $PlcPort; pageOrigin = $PageOrigin; source = 'Test/VirtualLoop'; workerOwner = 'Host/WorkerProcessSupervisor'; desktop = 'NotStarted'; configuration = [ordered]@{ public = 's01-public-virtual-loop/1.2.0'; budget = 's01-budget-virtual-loop/2.0.0'; simulation = 's01-sim-virtual-loop/2.0.0'; heartbeatDisconnectMs = 3000; plcIoTimeoutMs = 1000; imageManifestSha256 = (Get-FileHash $imageManifest -Algorithm SHA256).Hash; algorithmManifestSha256 = (Get-FileHash $algorithmManifest -Algorithm SHA256).Hash; workerScriptSha256 = (Get-FileHash $workerScript -Algorithm SHA256).Hash } }
$record.configuration.public = "$($env:Gaode__PublicId)/$($env:Gaode__PublicVersion)"
$record.allowedTestRoot = $allowed
$record.testPersistenceFaultCase = $TestPersistenceFaultCase
$record.configuration.budget = "$($env:Gaode__BudgetId)/$($env:Gaode__BudgetVersion)"
$record.configuration.simulation = "$($env:Gaode__SimulationId)/$($env:Gaode__SimulationVersion)"
if ($fixture) {
    $record.fixtureManifestSha256 = (Get-FileHash $FixtureManifest -Algorithm SHA256).Hash
    $record.caseId = $fixture.caseId
    $record.recipeRef = $fixture.recipeRef
    $record.configuration.recipeCatalogSha256 = (Get-FileHash $fixture.recipeCatalogPath -Algorithm SHA256).Hash
}
if (-not $fixture) {
    $record.configuration.publicSha256 = (Get-FileHash (Join-Path $feature 'examples/public.virtual-loop.json') -Algorithm SHA256).Hash
    $record.configuration.budgetSha256 = (Get-FileHash (Join-Path $feature 'examples/budget.virtual-loop.json') -Algorithm SHA256).Hash
    $record.configuration.simulationSha256 = (Get-FileHash (Join-Path $feature 'examples/simulation.virtual-loop.json') -Algorithm SHA256).Hash
}
$record.configuration.plcMapSha256 = (Get-FileHash (Join-Path $repo 'backend/src/Gaode.Plc.Protocol/Signals.cs') -Algorithm SHA256).Hash
$mapSource = Get-Content (Join-Path $repo 'backend/src/Gaode.Plc.Protocol/Signals.cs') -Raw
$record.configuration.protocolContract = [regex]::Match($mapSource, 'public const string Contract = "([^"]+)"').Groups[1].Value
$record.configuration.plcAcquisition = "plc-acquisition/013-1" # Fixed communication-layer policy.
$record.configuration.plcStateStaleAfterMs = 5000
$record.configuration.hostDllSha256 = (Get-FileHash $hostDll -Algorithm SHA256).Hash
$record.configuration.plcDllSha256 = (Get-FileHash $plcDll -Algorithm SHA256).Hash
$record.configuration.dotnetVersion = (& dotnet --version | Select-Object -First 1)
$record.configuration.windowsNativeThreadPool = [bool]$WindowsNativeThreadPool
$startedProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
try {
    $plcHealthy = $false
    try { $null = Invoke-RestMethod -Uri "$PlcApiBase/health" -TimeoutSec 2; $plcHealthy = $true } catch { }
    if (-not $plcHealthy) {
        $plcLaunchOptions = @{}
        if ($WindowsNativeThreadPool) { $plcLaunchOptions.Environment = @{ DOTNET_ThreadPool_UseWindowsThreadPool = '1'; DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS = '0' } }
        if ($PlcSocketInlineCompletions) {
            $plcLaunchOptions.Environment = @{ DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS = '1' }
            $record.configuration.plcSocketInlineCompletions = '1;ExplicitTestOwnPlcOnly'
        }
        $plc = Start-Process -FilePath 'dotnet' -ArgumentList ('"' + $plcDll + '" --urls ' + $PlcApiBase) -WorkingDirectory (Join-Path $repo 'VirtualPlc') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $TestRoot 'logs/plc.out.log') -RedirectStandardError (Join-Path $TestRoot 'logs/plc.err.log') @plcLaunchOptions
        $record.plcPid = $plc.Id
        $startedProcesses.Add($plc)
    } else { throw "PLC API port already in use: $PlcApiBase; use isolated ports" }
    # Start the consumer only after this owned simulator has actually started.
    # This preparation gate does not extend a Modbus exchange or heartbeat deadline.
    $plcReadyEnd = [datetime]::UtcNow.AddSeconds(30)
    $plcHealthy = $false
    while ([datetime]::UtcNow -lt $plcReadyEnd -and -not $plcHealthy) {
        if ($plc.HasExited) { throw 'VirtualPlc exited before readiness; inspect plc.err.log.' }
        try {
            $health = Invoke-RestMethod -Uri "$PlcApiBase/health" -TimeoutSec 2
            $plcHealthy = $health.service -eq 'VirtualPlc' -and $health.status -eq 'ok'
        } catch { }
        if (-not $plcHealthy) { Start-Sleep -Milliseconds 250 }
    }
    if (-not $plcHealthy) { throw 'Owned VirtualPlc failed its finite startup health gate.' }
    $record.plcReadyBeforeHostUtc = [datetime]::UtcNow.ToString('o')
    $hostLaunchOptions = @{}
    if ($WindowsNativeThreadPool) { $hostLaunchOptions.Environment = @{ DOTNET_ThreadPool_UseWindowsThreadPool = '1'; DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS = '0' } }
    if ($HostSocketInlineCompletions) {
        $hostLaunchOptions.Environment = @{ DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS = '1' }
        $record.configuration.hostSocketInlineCompletions = '1;ExplicitTestOwnHostOnly'
    }
    $hostProcess = Start-Process -FilePath 'dotnet' -ArgumentList ('"' + $hostDll + '" --urls ' + $ApiBase) -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $TestRoot 'logs/host.out.log') -RedirectStandardError (Join-Path $TestRoot 'logs/host.err.log') @hostLaunchOptions
    $record.hostPid = $hostProcess.Id
    $startedProcesses.Add($hostProcess)
    if ($fixture) {
        # Establish Test process scheduling before the first three-second heartbeat window.
        (Get-Process -Id $record.plcPid -ErrorAction Stop).PriorityClass = 'High'
        (Get-Process -Id $record.hostPid -ErrorAction Stop).PriorityClass = 'High'
        $record.scheduling = 'Test/HighBeforeReadiness'
    }
    $headers = @{ Authorization = "Bearer $OperatorToken" }
    $ready = $false
    for ($i = 0; $i -lt 120; $i++) {
        Start-Sleep -Milliseconds 250
        try { $null = Invoke-RestMethod -Uri "$PlcApiBase/health" -TimeoutSec 2; $plcHealthy = $true } catch { }
        if ($record.plcPid -and (Get-Process -Id $record.plcPid -ErrorAction SilentlyContinue) -eq $null) { throw 'VirtualPlc exited; inspect plc.err.log.' }
        if ($hostProcess.HasExited) { throw 'Host exited before readiness; inspect host.err.log.' }
        try {
            $status = Invoke-RestMethod -Uri "$ApiBase/api/v1/station01/status" -Headers $headers -TimeoutSec 2
            if ($plcHealthy -and $status.plc.schemaVersion -eq 'device-semantics/1' -and
                $status.plc.connection -eq 'Connected' -and
                ($DiagnosticEarlyReady -or ($status.algorithm.state -eq 'Ready' -and $status.camera.state -eq 'Ready'))) {
                $ready = $true; break
            }
        } catch { }
    }
    if (-not $ready) {
        $record.hostStatus = $status
        try { $record.plcState = Invoke-RestMethod -Uri "$PlcApiBase/api/simulator/state" -TimeoutSec 2 } catch { }
        try { $record.plcChanges = Invoke-RestMethod -Uri "$PlcApiBase/api/simulator/changes?after=0" -TimeoutSec 2 } catch { }
        throw 'Host or virtual components not ready; inspect logs and process.json status facts.'
    }
    $workers = @()
    try {
        $workers = @(Get-CimInstance Win32_Process -Filter "ParentProcessId = $($hostProcess.Id)" |
            Where-Object { $_.Name -match '^python(w)?\.exe$' })
    } catch {
        $record.workerOwnershipProbe = 'CimAccessDenied;HostWorkerSessionRequired'
    }
    if ($workers.Count -ne 1 -and -not $DiagnosticEarlyReady -and
        $record.workerOwnershipProbe -ne 'CimAccessDenied;HostWorkerSessionRequired') {
        throw "Expected exactly one Host-owned Python worker; found $($workers.Count)."
    }
    if ($record.workerOwnershipProbe -eq 'CimAccessDenied;HostWorkerSessionRequired' -and
        ($status.algorithm.state -ne 'Ready' -or $status.algorithm.reason -notlike 'session:*')) {
        throw 'Worker process ownership could not be queried and Host has no ready worker session.'
    }
    if ($record.workerOwnershipProbe -eq 'CimAccessDenied;HostWorkerSessionRequired') {
        # Use the actual owned Host's spawn/Ready records, not an inferred PID.
        # This is explicitly Host-reported ownership, not an OS parent query.
        $workerSession = [string]$status.algorithm.reason
        $sessionId = $workerSession.Substring('session:'.Length)
        $workerEvents = @(Get-Content -LiteralPath (Join-Path $TestRoot 'logs/host.out.log') |
            ForEach-Object {
                $offset = $_.IndexOf('RuntimeFlow {', [StringComparison]::Ordinal)
                if ($offset -ge 0) { $_.Substring($offset + 'RuntimeFlow '.Length) | ConvertFrom-Json }
            } | Where-Object { $_.processId -eq $hostProcess.Id -and $_.step -eq 'WorkerProcess' -and
                [string]$_.facts.workerSessionId -eq $sessionId })
        $workerStarted = @($workerEvents | Where-Object outcome -eq 'Started')
        $workerReady = @($workerEvents | Where-Object outcome -eq 'Ready')
        if ($workerStarted.Count -ne 1 -or $workerReady.Count -ne 1 -or
            $workerStarted[0].facts.processId -ne $workerReady[0].facts.processId) {
            throw 'Owned Host worker spawn and Ready identity records are incomplete.'
        }
        $actualWorker = Get-Process -Id ([int]$workerStarted[0].facts.processId) -ErrorAction Stop
        if ($actualWorker.ProcessName -notmatch '^python(w)?$' -or
            $actualWorker.StartTime.ToUniversalTime() -lt $hostProcess.StartTime.ToUniversalTime()) {
            throw 'Recorded Worker process is unavailable or predates this Host.'
        }
        $record.workerPid = $actualWorker.Id
        $record.workerSession = $workerSession
        $record.workerOwnershipProbe = 'OwnedHostRuntimeFlowStartedAndReady;LivePythonProcess;OsParentQueryUnavailable'
        $record.workerOwnershipEvidence = @($workerStarted[0], $workerReady[0])
    }
    if ($workers.Count -eq 1) { $record.workerPid = $workers[0].ProcessId; $record.workerSession = $status.algorithm.reason }
    if (-not $DiagnosticEarlyReady -and -not $fixture) {
        $watch = Start-Process -FilePath 'pwsh' -ArgumentList ('-NoProfile -File "' + (Join-Path $PSScriptRoot 'watch-station01-auto-removal.ps1') + '" -ApiBase "' + $ApiBase + '" -OutputDirectory "' + $TestRoot + '"') -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $TestRoot 'logs/watch.out.log') -RedirectStandardError (Join-Path $TestRoot 'logs/watch.err.log')
        $record.watchPid = $watch.Id
    } else { $record.watch = 'SkippedForReadinessDiagnostics;NoAutomaticManualConfirmation' }
    if (-not $SkipDesktop) {
        $desktop = Join-Path $repo 'desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe'
        if (Test-Path $desktop) {
            $app = Start-Process -FilePath $desktop -WorkingDirectory (Split-Path $desktop) -PassThru
            $record.desktopPid = $app.Id
            $record.desktop = 'Started;006 page/API delivery requires separate verification'
        } else { $record.desktop = 'NotReady;build existing desktop project' }
    }
    $record.hostStatus = $status
} catch {
    # A readiness failure occurs before the page runner can register ownership.
    # Close only processes started by this invocation; retain all failure facts.
    $record.startupFailure = $_.Exception.Message
    if ($record.hostPid) {
        try {
            $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId = $($record.hostPid)" |
                Where-Object { $_.Name -match '^python(w)?\.exe$' -and
                    $_.CommandLine.Contains($workerScript, [StringComparison]::OrdinalIgnoreCase) })
            foreach ($child in $children) { Stop-Process -Id $child.ProcessId -ErrorAction Stop }
        } catch { $record.workerCleanupFailure = $_.Exception.Message }
    }
    foreach ($startedProcess in $startedProcesses) {
        try {
            if (-not $startedProcess.HasExited) { $startedProcess.Kill(); $startedProcess.WaitForExit(5000) | Out-Null }
        } catch { $record.processCleanupFailure = $_.Exception.Message }
    }
    $record.startupCleanupAtUtc = [datetime]::UtcNow.ToString('o')
    throw
} finally {
    $record | ConvertTo-Json -Depth 12 | Set-Content -Encoding utf8 (Join-Path $TestRoot 'process.json')
    Write-Output (Join-Path $TestRoot 'process.json')
}
