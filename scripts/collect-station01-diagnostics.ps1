param(
    [Parameter(Mandatory = $true)][string]$TestRoot,
    [Parameter(Mandatory = $true)][string]$RequestId,
    [string]$CommandId = '',
    [string]$RunId = '',
    [string]$OperationId = ''
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/station01-007'))
$root = [IO.Path]::GetFullPath($TestRoot)
if (-not $root.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'TestRoot must be below artifacts/station01-007.'
}
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'TestRoot does not exist.' }
$ids = @($RequestId, $CommandId, $RunId, $OperationId) | Where-Object { $_ }
$files = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.Name -ne 'diagnostic-index.json' -and
    ($_.Extension -in @('.log', '.json', '.db', '.trx', '.png') -or
        $_.Name -in @('station01.test.db-wal', 'station01.test.db-shm'))
})
$index = @(
    foreach ($file in $files) {
        $matches = @()
        if ($file.Extension -in @('.log', '.json', '.trx')) {
            foreach ($id in $ids) {
                $hits = @(Select-String -LiteralPath $file.FullName -SimpleMatch -Pattern $id -ErrorAction SilentlyContinue)
                foreach ($hit in $hits) { $matches += [ordered]@{ id = $id; line = $hit.LineNumber } }
            }
        }
        [ordered]@{
            path = [IO.Path]::GetRelativePath($root, $file.FullName)
            bytes = $file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            correlatedLines = $matches
        }
    }
)
$process = $null
$processPath = Join-Path $root 'process.json'
if (Test-Path -LiteralPath $processPath) { $process = Get-Content -LiteralPath $processPath -Raw | ConvertFrom-Json }
$desktopCapture = $null
$desktopPath = Join-Path $root 'interactive-desktop.json'
if (Test-Path -LiteralPath $desktopPath) {
    $desktopCapture = Get-Content -LiteralPath $desktopPath -Raw | ConvertFrom-Json
}
$sqlite = $null
$databasePath = Join-Path $root 'station01.test.db'
if ($RunId -and (Test-Path -LiteralPath $databasePath)) {
    $query = Join-Path $PSScriptRoot 'query-station01-diagnostic.py'
    $sqlite = & python $query $databasePath $RunId | ConvertFrom-Json
}
$report = [ordered]@{
    schemaVersion = 'station01-diagnostic-index/1.0'
    collectedAtUtc = [datetime]::UtcNow.ToString('o')
    source = 'Test/VirtualLoop; SoftwareLoopOnly; RealPLCNotVerified'
    requestId = $RequestId
    commandId = $CommandId
    runId = $RunId
    operationId = $OperationId
    process = $process
    desktopCapture = $desktopCapture
    sqlite = $sqlite
    files = $index
    note = 'Line indexes and hashes refer to saved local evidence; raw exceptions remain in restricted Host/PLC logs. No automatic retry or safety inference is made by this index.'
}
$destination = Join-Path $root 'diagnostic-index.json'
$report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $destination -Encoding utf8
Write-Output $destination
