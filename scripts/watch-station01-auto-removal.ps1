param(
    [string]$ApiBase = 'http://127.0.0.1:5001',
    [string]$OperatorToken = $env:GAODE_TEST_OPERATOR_TOKEN,
    [string]$OutputDirectory = '',
    [int]$PollSeconds = 2
)
$ErrorActionPreference = 'Stop'
if (-not $OperatorToken) { throw 'Controlled Test Operator token is required.' }
if ($PollSeconds -lt 1) { throw 'PollSeconds must be positive.' }
if (-not $OutputDirectory) {
    $repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $OutputDirectory = Join-Path $repo 'artifacts/station01-007/auto-removal'
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$headers = @{ Authorization = "Bearer $OperatorToken" }
$done = [System.Collections.Generic.HashSet[string]]::new()
while ($true) {
    try {
        $status = Invoke-RestMethod -Uri "$ApiBase/api/v1/station01/status" -Headers $headers
        $runId = [string]$status.currentRun.runId
        if ($runId -and -not $done.Contains($runId)) {
            $run = Invoke-RestMethod -Uri "$ApiBase/api/v1/station01/runs/$runId" -Headers $headers
            if ($run.state -eq 16 -or $run.state -eq 'AwaitingManualRemoval') {
                $facts = Invoke-RestMethod -Uri "$ApiBase/api/v1/station01/runs/$runId/evidence" -Headers $headers
                $unlocked = @($facts.stages | Where-Object { $_.eventType -eq 'ObservedUnlocked' }).Count -gt 0
                if ($facts.wholeTrayCompletionId -and $unlocked) {
                    $requestId = 'auto-removal-' + $runId.Replace('-', '')
                    $body = @{ requestId = $requestId; expectedRevision = $run.observedRevision; reason = 'Test simulated tray removal after committed unlock' }
                    $entry = [ordered]@{ runId = $runId; request = $body; source = 'Test/ControlledCommissioningClient'; unlockObserved = $true; wholeTrayCompletionId = $facts.wholeTrayCompletionId; submittedAtUtc = [datetime]::UtcNow.ToString('o') }
                    try {
                        $entry.response = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/v1/station01/runs/$runId/manual-removal-confirmations" -Headers $headers -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress)
                        $entry.final = Invoke-RestMethod -Uri "$ApiBase/api/v1/station01/runs/$runId/evidence" -Headers $headers
                        if ($entry.final.finalResult -eq 'FinalUnloadCompletion') { [void]$done.Add($runId) }
                    } catch { $entry.error = $_.Exception.Message }
                    $entry | ConvertTo-Json -Depth 16 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory "auto-$runId.json")
                }
            }
        }
    } catch {
        [pscustomobject]@{ observedAtUtc = [datetime]::UtcNow.ToString('o'); error = $_.Exception.Message } |
            ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'last-watch-error.json')
    }
    Start-Sleep -Seconds $PollSeconds
}
