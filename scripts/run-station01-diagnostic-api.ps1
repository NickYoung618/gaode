param(
    [ValidateSet('UnsafeBeforeRequest', 'CommunicationAfterReceipt')][string]$Scenario,
    [string]$EvidenceRoot,
    [string]$ApiBase = 'http://127.0.0.1:5101',
    [string]$PlcApiBase = 'http://127.0.0.1:5180',
    [int]$PlcPort = 1602
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $EvidenceRoot) { $EvidenceRoot = Join-Path $repo ('artifacts/station01-007/api-diagnostic-' + $Scenario.ToLower() + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/station01-007'))
if (-not $EvidenceRoot.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'EvidenceRoot must be below artifacts/station01-007.' }
$token = [guid]::NewGuid().ToString('N')
$env:GAODE_TEST_OPERATOR_TOKEN = $token
$record = $null
$report = [ordered]@{ scenario = $Scenario; source = 'Test/VirtualLoop;SoftwareLoopOnly'; realDeviceVerified = $false;
    startedAtUtc = [datetime]::UtcNow.ToString('o'); apiBase = $ApiBase; plcApiBase = $PlcApiBase; plcPort = $PlcPort }
try {
    $recordPath = @(& (Join-Path $PSScriptRoot 'start-station01-virtual-loop.ps1') -SkipDesktop -DiagnosticEarlyReady `
        -TestRoot $EvidenceRoot -OperatorToken $token -ApiBase $ApiBase -PlcApiBase $PlcApiBase -PlcPort $PlcPort)[-1]
    $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
    $report.process = $record
    $loadPath = @(& (Join-Path $PSScriptRoot 'simulate-station01-load.ps1') -PrepareOnly `
        -OutputDirectory $EvidenceRoot -Scenario S1 -OccupiedSlots P01)[-1]
    $prepared = Get-Content -LiteralPath $loadPath -Raw | ConvertFrom-Json
    $report.requestId = $prepared.request.requestId
    $headers = @{ Authorization = "Bearer $token" }
    $report.statusBeforeFault = Invoke-RestMethod "$ApiBase/api/v1/station01/status" -Headers $headers
    if ($Scenario -eq 'UnsafeBeforeRequest') {
        $report.faultAtUtc = [datetime]::UtcNow.ToString('o')
        $report.fault = Invoke-RestMethod -Method Post "$PlcApiBase/api/simulator/faults/EmergencyAlarm"
        Start-Sleep -Milliseconds 300
        $report.statusBeforeRequest = Invoke-RestMethod "$ApiBase/api/v1/station01/status" -Headers $headers
    }
    $response = Invoke-WebRequest -Method Post "$ApiBase/api/v1/station01/runs" -Headers $headers `
        -ContentType 'application/json' -Body ($prepared.request | ConvertTo-Json -Compress -Depth 10) -SkipHttpErrorCheck
    $report.httpStatus = [int]$response.StatusCode
    $report.receiptAtUtc = [datetime]::UtcNow.ToString('o')
    $report.receipt = $response.Content | ConvertFrom-Json
    if ($response.StatusCode -eq 202) {
        $report.commandId = $report.receipt.commandId
        $report.runId = $report.receipt.runId
    }
    if ($Scenario -eq 'CommunicationAfterReceipt') {
        $report.faultAtUtc = [datetime]::UtcNow.ToString('o')
        $report.fault = Invoke-RestMethod -Method Post "$PlcApiBase/api/simulator/faults/PauseHeartbeat"
    }
    Start-Sleep -Seconds 5
    if ($report.runId) {
        $report.run = Invoke-RestMethod "$ApiBase/api/v1/station01/runs/$($report.runId)" -Headers $headers
        $report.command = Invoke-RestMethod "$ApiBase/api/v1/station01/commands/$($report.commandId)" -Headers $headers
    }
    $report.statusAfter = Invoke-RestMethod "$ApiBase/api/v1/station01/status" -Headers $headers
    $report.plcState = Invoke-RestMethod "$PlcApiBase/api/simulator/state"
    $report.plcAudit = Invoke-RestMethod "$PlcApiBase/api/simulator/audit"
    $report.plcChanges = Invoke-RestMethod "$PlcApiBase/api/simulator/changes?after=0"
    $report.checks = [ordered]@{
        faultAfterReceipt = $Scenario -eq 'CommunicationAfterReceipt' -and $report.runId -and
            ([datetime]$report.faultAtUtc -gt [datetime]$report.receiptAtUtc)
        noCapture = $report.run -and $report.run.capture -eq 0
        noAlgorithm = $report.run -and $report.run.algorithm -eq 0
    }
    $report.completedAtUtc = [datetime]::UtcNow.ToString('o')
} catch {
    $report.error = $_.Exception.ToString()
    throw
} finally {
    if (-not $record -and (Test-Path -LiteralPath (Join-Path $EvidenceRoot 'process.json'))) {
        $record = Get-Content -LiteralPath (Join-Path $EvidenceRoot 'process.json') -Raw | ConvertFrom-Json
    }
    if ($record) {
        foreach ($ownedId in @($record.watchPid, $record.hostPid, $record.workerPid, $record.plcPid)) {
            if ($ownedId -and (Get-Process -Id $ownedId -ErrorAction SilentlyContinue)) {
                Stop-Process -Id $ownedId -ErrorAction SilentlyContinue
            }
        }
    }
    $report | ConvertTo-Json -Depth 25 | Set-Content -LiteralPath (Join-Path $EvidenceRoot 'api-diagnostic-transcript.json') -Encoding utf8
    if ($report.requestId) {
        & (Join-Path $PSScriptRoot 'collect-station01-diagnostics.ps1') -TestRoot $EvidenceRoot `
            -RequestId $report.requestId -CommandId $report.commandId -RunId $report.runId | Out-Null
    }
    Remove-Item Env:GAODE_TEST_OPERATOR_TOKEN -ErrorAction SilentlyContinue
    if (-not $report.error) {
        $wireKind = if ($Scenario -eq 'CommunicationAfterReceipt') { 'communication' } else { 'unsafe' }
        & node (Join-Path $PSScriptRoot 'communication/check-009-diagnostic-wire.cjs') `
            (Join-Path $EvidenceRoot 'api-diagnostic-transcript.json') $wireKind `
            (Join-Path $EvidenceRoot 'diagnostic-wire-validation.json')
        if ($LASTEXITCODE -ne 0) { throw 'Independent diagnostic communication evidence failed.' }
    }
    Write-Output $EvidenceRoot
}
