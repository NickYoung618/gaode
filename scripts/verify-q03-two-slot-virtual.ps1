param([Parameter(Mandatory)][string]$EvidenceRoot)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/recipe-execution-008'))
$root = [IO.Path]::GetFullPath($EvidenceRoot)
if (-not $root.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $root)) {
    throw 'EvidenceRoot must be new under the isolated 008 artifacts directory.'
}
New-Item -ItemType Directory -Path $root | Out-Null
$ports = @(25172,25173,25174)
foreach ($port in $ports) {
    if (@(netstat -ano -p TCP | Select-String "^\s*TCP\s+\S+:$port\s+\S+\s+LISTENING\s+").Count -gt 0) {
        throw "Isolated port in use: $port"
    }
}
$fixture = Join-Path $repo 'specs/008-recipe-driven-inspection/fixtures/fixture-q03-two-slot.json'
$api = "http://127.0.0.1:$($ports[0])"
$plcApi = "http://127.0.0.1:$($ports[1])"
$token = [guid]::NewGuid().ToString('N')
$env:GAODE_TEST_OPERATOR_TOKEN = $token
$env:Simulation__ScanPeriodMs = '20'
$phase = 'platform'; $platform = $null; $runId = $null; $exitCode = 1
try {
    $null = & (Join-Path $repo 'scripts/start-station01-virtual-loop.ps1') `
        -OperatorToken $token -TestRoot $root -ApiBase $api -PlcApiBase $plcApi `
        -PlcPort $ports[2] -HostDll (Join-Path $repo 'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll') `
        -PlcDll (Join-Path $repo 'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll') `
        -FixtureManifest $fixture -SkipDesktop
    $platform = Get-Content (Join-Path $root 'process.json') -Raw | ConvertFrom-Json
    $phase = 'prepare'
    $preparedPath = [string](& (Join-Path $repo 'scripts/simulate-station01-load.ps1') `
        -PrepareOnly -OutputDirectory $root -ApiBase $api -FixtureManifest $fixture)
    $prepared = Get-Content $preparedPath -Raw | ConvertFrom-Json
    $headers = @{ Authorization = "Bearer $token" }
    $phase = 'start'
    $receipt = Invoke-RestMethod -Method Post -Uri "$api/api/v1/station01/runs" `
        -Headers $headers -ContentType 'application/json' `
        -Body ($prepared.request | ConvertTo-Json -Compress -Depth 12)
    $runId = [string]$receipt.runId
    @{ source = 'BackendDiagnosticOnly_NotPageEvidence'; prepared = $preparedPath; receipt = $receipt } |
        ConvertTo-Json -Depth 12 | Set-Content (Join-Path $root 'backend-diagnostic-start.json') -Encoding utf8
    $phase = 'observe'
    $deadline = (Get-Date).AddMinutes(8)
    do {
        Start-Sleep -Seconds 2
        $run = Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $headers
        if (@($run.events) -contains 'AwaitingManualTrayRemoval' -or
            $run.errorCode -or @($run.events) -contains 'FinalUnloadCompleted') { break }
    } while ((Get-Date) -lt $deadline)
    $facts = [ordered]@{
        source = 'BackendDiagnosticOnly_NotPageEvidence'; caseId = 'Q03-TwoSlot'
        runId = $runId; run = $run
        evidence = Invoke-RestMethod "$api/api/v1/station01/runs/$runId/evidence" -Headers $headers
        plcState = Invoke-RestMethod "$plcApi/api/simulator/state"
        plcChanges = Invoke-RestMethod "$plcApi/api/simulator/changes?after=0"
    }
    $facts | ConvertTo-Json -Depth 80 | Set-Content (Join-Path $root 'backend-diagnostic-evidence.json') -Encoding utf8
    if (@($run.events) -notcontains 'AwaitingManualTrayRemoval' -or $run.errorCode) {
        throw "Two-slot run did not reach unlocked removal state: $($run.errorCode)"
    }
    $exitCode = 0
} catch {
    @{ phase = $phase; error = $_.Exception.ToString(); runId = $runId } |
        ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'validation-error.json') -Encoding utf8
} finally {
    if (-not $platform -and (Test-Path (Join-Path $root 'process.json'))) {
        $platform = Get-Content (Join-Path $root 'process.json') -Raw | ConvertFrom-Json
    }
    if ($platform) {
        foreach ($id in @($platform.hostPid, $platform.plcPid)) {
            if (-not $id) { continue }
            $owned = Get-Process -Id ([int]$id) -ErrorAction SilentlyContinue
            if ($owned -and [string]::Equals([string]$owned.Path,
                [string](Get-Command dotnet).Source, [StringComparison]::OrdinalIgnoreCase)) {
                Stop-Process -Id ([int]$id) -ErrorAction SilentlyContinue
            }
        }
    }
    Remove-Item Env:GAODE_TEST_OPERATOR_TOKEN -ErrorAction SilentlyContinue
    @{ exitCode = $exitCode; phase = $phase; runId = $runId; realDeviceVerified = $false } |
        ConvertTo-Json | Set-Content (Join-Path $root 'validation-result.json') -Encoding utf8
}
if ($exitCode -ne 0) { throw "Q03 two-slot virtual verification stopped at $phase; inspect $root" }
Write-Output (Join-Path $root 'backend-diagnostic-evidence.json')
