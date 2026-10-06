param(
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [Parameter(Mandatory)][string]$OperatorToken,
    [Parameter(Mandatory)][string]$ApiBase,
    [Parameter(Mandatory)][string]$PlcApiBase
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$root = [IO.Path]::GetFullPath($EvidenceRoot)
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/station01-007'))
if (-not $root.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)) { throw 'EvidenceRoot outside isolated Test evidence.' }
$page = Get-Content -LiteralPath (Join-Path $root 'webview2-page-evidence.json') -Raw | ConvertFrom-Json
$post = @($page.network | Where-Object { $_.method -eq 'POST' -and $_.url -like '*/api/v1/station01/runs' })[0]
if (-not $post -or -not $post.responseBody.runId) { throw 'Actual WebView2 POST 202 receipt missing.' }
$runId = [string]$post.responseBody.runId
$commandId = [string]$post.responseBody.commandId
$requestId = [string]$post.requestBody.requestId
$headers = @{ Authorization = "Bearer $OperatorToken" }
$facts = [ordered]@{
    collectedAtUtc = [datetime]::UtcNow.ToString('o')
    source = 'Test/WPF-WebView2;SoftwareLoopOnly'
    realDeviceVerified = $false
    requestId = $requestId
    commandId = $commandId
    runId = $runId
    pageEvidence = 'webview2-page-evidence.json'
    pageEvidenceSha256 = (Get-FileHash (Join-Path $root 'webview2-page-evidence.json') -Algorithm SHA256).Hash
    desktopEvidence = 'interactive-desktop.json'
    desktopEvidenceSha256 = (Get-FileHash (Join-Path $root 'interactive-desktop.json') -Algorithm SHA256).Hash
    run = Invoke-RestMethod "$ApiBase/api/v1/station01/runs/$runId" -Headers $headers
    command = Invoke-RestMethod "$ApiBase/api/v1/station01/commands/$commandId" -Headers $headers
    status = Invoke-RestMethod "$ApiBase/api/v1/station01/status" -Headers $headers
    plcState = Invoke-RestMethod "$PlcApiBase/api/simulator/state"
    plcAudit = Invoke-RestMethod "$PlcApiBase/api/simulator/audit"
    plcChanges = Invoke-RestMethod "$PlcApiBase/api/simulator/changes?after=0"
}
$facts | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $root 'page-api-device-facts.json') -Encoding utf8
# The hash/index collector runs after Host/SQLite exit, not while WAL files are live.
Join-Path $root 'page-api-device-facts.json'
