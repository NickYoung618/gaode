param(
    [switch]$PrepareOnly,
    [switch]$StartRun,
    [string]$ApiBase = 'http://127.0.0.1:5001',
    [string]$OperatorToken = $env:GAODE_TEST_OPERATOR_TOKEN,
    [string]$OutputDirectory = '',
    [string]$FixtureManifest = '',
    [string]$Scenario = 'S1',
    [string[]]$OccupiedSlots = @('P01')
)
$ErrorActionPreference = 'Stop'
if ($PrepareOnly -and $StartRun) { throw 'PrepareOnly and StartRun are mutually exclusive.' }
if (-not $PrepareOnly -and -not $StartRun) { throw 'Specify PrepareOnly or StartRun.' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$fixture = $null
if ($FixtureManifest) {
    if (-not [IO.Path]::IsPathFullyQualified($FixtureManifest)) { throw 'FixtureManifest must be absolute.' }
    $FixtureManifest = [IO.Path]::GetFullPath($FixtureManifest)
    $fixture = Get-Content -LiteralPath $FixtureManifest -Raw | ConvertFrom-Json -AsHashtable
    if ($StartRun) { throw 'FixtureManifest is PrepareOnly; run start belongs to the formal frontend.' }
    if ($fixture.purpose -ne 'Test' -or -not $fixture.caseId -or
        -not $fixture.recipeRef.recipeId -or -not $fixture.recipeRef.version -or
        -not $fixture.recipeRef.catalogDigest -or
        $fixture.scenarioId -notin @('S1','S2','S3') -or
        @($fixture.occupiedSlots).Count -lt 1 -or
        @($fixture.occupiedSlots | Select-Object -Unique).Count -ne @($fixture.occupiedSlots).Count) {
        throw 'Invalid FixtureManifest Test identity, scenario or slots.'
    }
    $Scenario = $fixture.scenarioId
    $OccupiedSlots = @($fixture.occupiedSlots)
    $catalogPath = [string]$fixture.recipeCatalogPath
    if (-not [IO.Path]::IsPathFullyQualified($catalogPath) -or
        -not (Test-Path -LiteralPath $catalogPath) -or
        (Get-FileHash -LiteralPath $catalogPath -Algorithm SHA256).Hash -ne [string]$fixture.recipeRef.catalogDigest) {
        throw 'FixtureManifest recipe catalog missing or digest differs.'
    }
} elseif ($Scenario -ne 'S1' -or $OccupiedSlots.Count -ne 1 -or $OccupiedSlots[0] -ne 'P01') {
    throw 'Historical 007 fixture only supports S1/CAP/P01.'
}
$config = if ($fixture) { $fixture } else {
    Get-Content -Raw (Join-Path $repo 'specs/007-station01-integrated-loop/examples/virtual-loop.json') | ConvertFrom-Json
}
if ($config.purpose -ne 'Test') { throw 'Invalid Test fixture purpose.' }
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repo $(if ($fixture) { 'artifacts/recipe-execution-008/prepared' } else { 'artifacts/station01-007/prepared' })
}
if ($fixture) {
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    $allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/recipe-execution-008'))
    if (-not $OutputDirectory.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'FixtureManifest output must remain under artifacts/recipe-execution-008.'
    }
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$context = [ordered]@{
    schemaVersion = $(if ($fixture) { 'station01-start-run-context/2.0' } else { 'station01-start-run-context/1.0' })
    trayId = [guid]::NewGuid().ToString()
    stationId = [guid]::NewGuid().ToString()
    lineId = [guid]::NewGuid().ToString()
    scenarioId = $Scenario
    occupiedSlots = @($OccupiedSlots)
    purpose = 'Test'
}
if ($fixture) { $context.expectedRecipeRef = $fixture.recipeRef }
$requestId = 's01-007-' + [guid]::NewGuid().ToString('N')
$body = [ordered]@{
    requestId = $requestId
    contextJson = ($context | ConvertTo-Json -Compress -Depth 8)
    publicConfigRef = $(if ($fixture) { $fixture.publicConfigRef } else { @{ id = 's01-public-virtual-loop'; version = '1.2.0' } })
    budgetRef = $(if ($fixture) { $fixture.budgetRef } else { @{ id = 's01-budget-virtual-loop'; version = '3.0.0' } })
    simulationRef = $(if ($fixture) { $fixture.simulationRef } else { @{ id = 's01-sim-virtual-loop'; version = '3.0.0' } })
}
$record = [ordered]@{ source = 'Test/PreparedLoad'; channel = $(if ($StartRun) { 'AuxiliaryApi' } else { 'PrepareOnlyFor006' }); fixture = $config; request = $body; createdAtUtc = [datetime]::UtcNow.ToString('o') }
if ($fixture) { $record.fixtureManifestSha256 = (Get-FileHash $FixtureManifest -Algorithm SHA256).Hash }
if ($StartRun) {
    if (-not $OperatorToken) { throw 'GAODE_TEST_OPERATOR_TOKEN or OperatorToken is required.' }
    $headers = @{ Authorization = "Bearer $OperatorToken" }
    $receipt = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/v1/station01/runs" -Headers $headers -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress -Depth 10)
    $record.receipt = $receipt
    $record.status = Invoke-RestMethod -Uri "$ApiBase/api/v1/station01/commands/$($receipt.commandId)" -Headers $headers
}
$path = Join-Path $OutputDirectory ("load-$requestId.json")
$record | ConvertTo-Json -Depth 16 | Set-Content -Encoding utf8 $path
Write-Output $path
