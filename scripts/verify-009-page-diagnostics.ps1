param(
    [Parameter(Mandatory)][string]$CommunicationRoot,
    [Parameter(Mandatory)][string]$UnsafeRoot,
    [Parameter(Mandatory)][string]$OutputDirectory
)
# Read-only checks of exited-process packages. Keep page/business and wire
# verdicts separate; either nonzero exit rejects the combined acceptance.
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new evidence output directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
& node (Join-Path $PSScriptRoot 'verify-station01-page-diagnostics.cjs') `
    $CommunicationRoot $UnsafeRoot (Join-Path $output 'business-page.json')
if ($LASTEXITCODE -ne 0) { throw 'Page/business evidence rejected.' }
foreach ($entry in @(@{ Root = $CommunicationRoot; Kind = 'communication' }, @{ Root = $UnsafeRoot; Kind = 'unsafe' })) {
    & node (Join-Path $PSScriptRoot 'communication/check-009-diagnostic-wire.cjs') `
        (Join-Path $entry.Root 'page-api-device-facts.json') $entry.Kind `
        (Join-Path $output ($entry.Kind + '-wire.json'))
    if ($LASTEXITCODE -ne 0) { throw ('Independent communication evidence rejected: ' + $entry.Kind) }
}
Write-Output $output
