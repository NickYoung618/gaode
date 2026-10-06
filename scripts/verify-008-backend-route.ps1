param([Parameter(Mandatory)][ValidatePattern('^Q(0[4-9]|1[0-9]|2[0-2])$')][string]$Case,
      [Parameter(Mandatory)][string]$EvidenceRoot,
      [string]$Fault = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = [IO.Path]::GetFullPath($EvidenceRoot)
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/recipe-execution-008'))
if (-not $root.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase) -or (Test-Path $root)) {
    throw 'EvidenceRoot must be new under isolated 008 artifacts.'
}
$fixture = Join-Path $repo "specs/008-recipe-driven-inspection/fixtures/fixture-$($Case.ToLowerInvariant()).json"
$api = 'http://127.0.0.1:25202'; $plcApi = 'http://127.0.0.1:25203'
$token = [guid]::NewGuid().ToString('N')
$platform = $null
$phase = 'preflight'; $runId = $null
try {
    $phase = 'platform'
    $null = & (Join-Path $repo 'scripts/start-station01-virtual-loop.ps1') `
        -OperatorToken $token -TestRoot $root -ApiBase $api -PlcApiBase $plcApi `
        -PlcPort 25204 -FixtureManifest $fixture -SkipDesktop
    $platform = Get-Content (Join-Path $root 'process.json') -Raw | ConvertFrom-Json
    $phase = 'prepare'
    $prepared = & (Join-Path $repo 'scripts/simulate-station01-load.ps1') `
        -PrepareOnly -OutputDirectory $root -ApiBase $api -FixtureManifest $fixture
    $load = Get-Content $prepared -Raw | ConvertFrom-Json
    if ($Fault) {
        $phase = 'fault'
        $faultResult = Invoke-RestMethod -Method Post "$plcApi/api/simulator/faults/$Fault"
        $faultResult | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'fault.json')
    }
    $headers = @{ Authorization = "Bearer $token" }
    $phase = 'start-backend-only'
    $receipt = Invoke-RestMethod -Method Post "$api/api/v1/station01/runs" -Headers $headers `
        -ContentType 'application/json' -Body ($load.request | ConvertTo-Json -Depth 16 -Compress)
    $runId = [string]$receipt.runId
    $receipt | ConvertTo-Json -Depth 16 | Set-Content (Join-Path $root 'receipt.json')
    $phase = 'wait'
    $deadline = (Get-Date).AddMinutes(12)
    do {
        Start-Sleep -Seconds 2
        $run = Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $headers
        $run | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $root 'run-latest.json')
        if (@($run.allowedActions) -contains 'ConfirmManualTrayRemoval') { break }
        if ($run.errorCode -or $run.finalOutcome -ne 0) { break }
    } while ((Get-Date) -lt $deadline)
    if (@($run.allowedActions) -contains 'ConfirmManualTrayRemoval') {
        $phase = 'confirm-backend-only'
        $confirmation = Invoke-RestMethod -Method Post `
            "$api/api/v1/station01/runs/$runId/manual-removal-confirmations" -Headers $headers `
            -ContentType 'application/json' -Body (@{requestId=[guid]::NewGuid().ToString();
                expectedRevision=$run.observedRevision;reason='Test backend-only virtual tray removed'} |
                ConvertTo-Json -Compress)
        $confirmation | ConvertTo-Json -Depth 16 | Set-Content (Join-Path $root 'confirmation.json')
        Start-Sleep -Seconds 2
        $run = Invoke-RestMethod "$api/api/v1/station01/runs/$runId" -Headers $headers
        $run | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $root 'run-latest.json')
    }
    $phase = 'facts'
    Invoke-RestMethod "$api/api/v1/station01/runs/$runId/evidence" -Headers $headers |
        ConvertTo-Json -Depth 40 | Set-Content (Join-Path $root 'run-evidence.json')
    Invoke-RestMethod "$plcApi/api/simulator/changes?after=0" |
        ConvertTo-Json -Depth 40 | Set-Content (Join-Path $root 'plc-changes.json')
    @{caseId=$Case;runId=$runId;source='BackendAPI/TestOnly';pageVerified=$false;
      finalOutcome=$run.finalOutcome;state=$run.state;errorCode=$run.errorCode;
      phase=$phase;fault=$Fault;atUtc=[datetime]::UtcNow.ToString('o')} |
        ConvertTo-Json | Set-Content (Join-Path $root 'result.json')
    Write-Output (Join-Path $root 'result.json')
} catch {
    if (Test-Path $root) {
        @{caseId=$Case;runId=$runId;phase=$phase;error=$_.Exception.ToString();
          atUtc=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 10 |
            Set-Content (Join-Path $root 'validation-error.json')
    }
    throw
} finally {
    if ($platform) {
        foreach ($ownedId in @($platform.hostPid,$platform.plcPid)) {
            $process = Get-Process -Id ([int]$ownedId) -ErrorAction SilentlyContinue
            if ($process -and $process.Path -eq (Get-Command dotnet).Source) {
                Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
            }
        }
    }
}
