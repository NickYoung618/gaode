param([Parameter(Mandatory)][string]$CaseId, [string]$BinaryRoot = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$name = switch -CaseSensitive ($CaseId) {
    'Q01' { 'fixture.json' }
    'Q01-PARAM' { 'fixture-q01-param.json' }
    'Q03-Pending' { 'fixture-q03-pending.json' }
    'ASSEMBLY-A-E-NOCODE' { 'fixture-assembly-a-e-no-code.json' }
    default { 'fixture-' + $CaseId.ToLowerInvariant() + '.json' }
}
$fixturePath = Join-Path $repo "specs/008-recipe-driven-inspection/fixtures/$name"
if ($CaseId -in @('GROUP-F-PENDING','ASSEMBLY-A-E-PENDING','Q01-NG','Q02-PENDING','RECOVERY-F','Q01-PAUSE')) {
    $fixturePath = Join-Path $repo "specs/008-recipe-driven-inspection/fixtures/night-closure-20260926/$name"
}
if ($CaseId -eq 'GROUP-A-E' -or $CaseId -match '^Q(0[4-9]|1[0-9]|2[0-2])$') {
    if ($CaseId -in @('Q07','Q10','Q12','Q13','Q16','Q17','Q19','Q22')) { throw 'Recipe exited current business scope.' }
    $fixturePath = Join-Path $repo "specs/008-recipe-driven-inspection/fixtures/usr-e-1.0.2/$name"
}
if ($CaseId -eq 'Q02-PENDING-P03') {
    $fixturePath = Join-Path $repo 'specs/008-recipe-driven-inspection/fixtures/disposition-p03-1.1.4/fixture-q02-pending-p03.json'
}
$fixture = Get-Content -LiteralPath $fixturePath -Raw | ConvertFrom-Json
if (-not $BinaryRoot) { $BinaryRoot = Join-Path $repo 'backend/src/Gaode.Host/bin/Debug/net10.0' }
$binaryRoot = [IO.Path]::GetFullPath($BinaryRoot)
if (-not $binaryRoot.StartsWith($repo + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Budget assemblies must be from this local project Test build.'
}
foreach ($assembly in @('Gaode.Domain','Gaode.Application','Gaode.Infrastructure')) {
    $expectedPath = Join-Path $binaryRoot "$assembly.dll"
    $loaded = [Reflection.Assembly]::LoadFrom($expectedPath)
    if ($loaded.Location -ne $expectedPath) { throw 'Different budget build already loaded; automatic worker handoff required.' }
}
$catalog = [Gaode.Infrastructure.Recipes.JsonRecipeCatalog]::new($fixture.recipeCatalogPath)
if ($catalog.Digest -ne $fixture.recipeRef.catalogDigest) { throw 'Fixture catalog digest mismatch.' }
$plan = [Gaode.Application.Recipes.RecipeRunPlanner]::BuildExecutable($catalog, 'budget-preflight',
    $fixture.scenarioId, $fixture.fCode, [string[]]$fixture.occupiedSlots)
$budgetPath = Join-Path $fixture.configRoot 'budget.virtual-loop.json'
$options = [System.Text.Json.JsonSerializerOptions]::new()
$options.PropertyNameCaseInsensitive = $true
$budget = [System.Text.Json.JsonSerializer]::Deserialize(
    [IO.File]::ReadAllText($budgetPath), [Gaode.Domain.Configuration.BusinessBudget], $options)
if ($budget.Id -ne $fixture.budgetRef.id -or $budget.Version -ne $fixture.budgetRef.version) { throw 'Fixture budget identity mismatch.' }
$start = [DateTimeOffset]::UtcNow
$deadlines = [Gaode.Application.Workflow.RecipeExecutionBudget]::Freeze($plan, $budget, $start)
$routeMs = [long]($deadlines.SortingDeadlineUtc - $start).TotalMilliseconds
$publicMs = [long]($budget.BusinessMs.PlcAcceptance * 4 + $budget.BusinessMs.ClampCompletion +
    $budget.BusinessMs.XyCompletion * 2 + $budget.BusinessMs.Capture3d + $budget.BusinessMs.HeightAlgorithm +
    $budget.BusinessMs.CaptureF + $budget.BusinessMs.FDecode + $budget.BusinessMs.CriticalSave * 12)
# Recovery executes public preparation twice; preserve its old failure plus the complete new route.
if ($CaseId -in @('RECOVERY-3D','RECOVERY-F')) { $publicMs *= 2 }
$collectorMs = $routeMs + $publicMs + 120000 # page login/confirmation, refresh, reopen and screenshots
[pscustomobject]@{
    caseId = $CaseId; fixture = $fixturePath; formulaVersion = $deadlines.FormulaVersion
    routeMs = $routeMs; publicPreparationMs = $publicMs; pageOverheadMs = 120000
    collectorWaitMs = $collectorMs; jobTimeoutMs = $collectorMs + 180000 # startup, export and cleanup
    catalogSha256 = $catalog.Digest; budgetSha256 = (Get-FileHash $budgetPath).Hash
    applicationSha256 = (Get-FileHash (Join-Path $binaryRoot 'Gaode.Application.dll')).Hash
    businessTimeoutsChanged = $false
}
