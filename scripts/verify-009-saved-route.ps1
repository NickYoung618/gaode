param(
    [Parameter(Mandatory)][ValidateSet('Route','FlipTimeout','Scene','Summary')][string]$Mode,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [string]$Sequence,
    [int]$Slots = 1,
    [switch]$Page,
    [string]$SecondEvidenceRoot,
    [string]$ParameterEvidenceRoot
)
# No process/device/DB writes. Each child reads an exited-process evidence copy;
# its new report is not a substitute for the other child's independent result.
$ErrorActionPreference = 'Stop'
$probe = Join-Path $PSScriptRoot 'communication/check-009-route-wire.py'
switch ($Mode) {
    'Route' {
        if (-not $Sequence -or $Slots -lt 1) { throw 'Sequence and positive Slots are required.' }
        $extra = @('--sequence', $Sequence, '--slots', "$Slots")
        if ($Page) { $extra += '--page' }
        & python (Join-Path $PSScriptRoot 'validate-008-route-evidence.py') $EvidenceRoot @extra
        if ($LASTEXITCODE -ne 0) { throw 'Route business evidence rejected.' }
        $extra = @(); if ($Page) { $extra += '--page' }
        & python $probe route $EvidenceRoot @extra
        if ($LASTEXITCODE -ne 0) { throw 'Route communication evidence rejected.' }
    }
    'FlipTimeout' {
        & python (Join-Path $PSScriptRoot 'validate-008-flip-timeout.py') $EvidenceRoot
        if ($LASTEXITCODE -ne 0) { throw 'Timeout business evidence rejected.' }
        & python $probe flip-timeout $EvidenceRoot
        if ($LASTEXITCODE -ne 0) { throw 'Timeout communication evidence rejected.' }
    }
    'Scene' {
        & python (Join-Path $PSScriptRoot 'audit-008-night-page-route.py') $EvidenceRoot
        if ($LASTEXITCODE -ne 0) { throw 'Scene business evidence rejected.' }
        & python $probe scene $EvidenceRoot
        if ($LASTEXITCODE -ne 0) { throw 'Scene communication evidence rejected.' }
    }
    'Summary' {
        if (-not $SecondEvidenceRoot) { throw 'Summary requires both actual Q01 and Q02 roots.' }
        $roots = @($EvidenceRoot, $SecondEvidenceRoot)
        if ($ParameterEvidenceRoot) { $roots += $ParameterEvidenceRoot }
        & python (Join-Path $PSScriptRoot 'summarize-q01-q02-evidence.py') @roots
        if ($LASTEXITCODE -ne 0) { throw 'Summary business evidence rejected.' }
        foreach ($root in $roots) {
            & python $probe summary $root
            if ($LASTEXITCODE -ne 0) { throw ('Summary communication evidence rejected: ' + $root) }
        }
    }
}
