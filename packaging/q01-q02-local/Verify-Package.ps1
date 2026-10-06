# Backend/PLC startup smoke for the staged archive. No WPF action is simulated.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts/q01-q02-local-package/gaode-q01-q02-local-test'))
foreach ($case in @('Q01', 'Q02')) {
    $suffix = if ($case -eq 'Q01') { '' } else { '-q02' }
    $dir = Join-Path $root 'specs/008-recipe-driven-inspection/fixtures'
    $fixture = Get-Content -LiteralPath (Join-Path $dir "fixture$suffix.json") -Raw | ConvertFrom-Json -AsHashtable
    $fixture.configRoot = Join-Path $root 'specs/007-station01-integrated-loop/examples'
    $fixture.recipeCatalogPath = Join-Path $dir "recipes$suffix.json"
    $fixture.imageManifestPath = Join-Path $dir "media-manifest$suffix.json"
    $fixture.workerManifestPath = Join-Path $dir "worker-manifest$suffix.json"
    $fixture.workerScriptPath = Join-Path $root 'scripts/virtual-station01-algorithm.py'
    $fixturePath = Join-Path $root "artifacts/recipe-execution-008/smoke-$case-fixture.json"
    New-Item -ItemType Directory -Path (Split-Path $fixturePath) -Force | Out-Null
    $fixture | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $fixturePath -Encoding utf8
    $run = Join-Path $root ("artifacts/recipe-execution-008/smoke-$case-" + [guid]::NewGuid().ToString('N'))
    $token = [guid]::NewGuid().ToString('N')
    $recordPath = Join-Path $run 'process.json'
    try {
        & (Join-Path $root 'scripts/start-station01-virtual-loop.ps1') -OperatorToken $token `
            -FixtureManifest $fixturePath -TestRoot $run -SkipDesktop | Out-Null
        $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
        $headers = @{ Authorization = "Bearer $token" }
        $status = Invoke-RestMethod 'http://127.0.0.1:5001/api/v1/station01/status' -Headers $headers
        $plc = Invoke-RestMethod 'http://127.0.0.1:5080/health'
        $load = & (Join-Path $root 'scripts/simulate-station01-load.ps1') `
            -PrepareOnly -FixtureManifest $fixturePath -OutputDirectory $run
        if (-not $record.workerPid -or -not $status.plc.connected -or -not $load -or -not $plc) {
            throw "$case startup smoke incomplete."
        }
        Write-Host "$case backend/PLC/worker/PrepareOnly smoke passed."
    } finally {
        if (Test-Path -LiteralPath $recordPath) {
            $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
            foreach ($key in @('hostPid', 'workerPid', 'plcPid')) {
                if ($record.$key) {
                    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $([int]$record.$key)" -ErrorAction SilentlyContinue
                    if ($process -and ([string]$process.CommandLine).Contains($root, [StringComparison]::OrdinalIgnoreCase)) {
                        Stop-Process -Id ([int]$record.$key) -ErrorAction SilentlyContinue
                    }
                }
            }
        }
    }
}
