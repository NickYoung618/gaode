param([Parameter(Mandatory)][string]$BasePackagePath,
      [Parameter(Mandatory)][string]$UpdatedPackagePath,
      [Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
$baseRoot=[IO.Path]::GetFullPath($BasePackagePath)
$updatedRoot=[IO.Path]::GetFullPath($UpdatedPackagePath)
$patchRoot=[IO.Path]::GetFullPath($OutputPath)
if(Test-Path -LiteralPath $patchRoot){throw 'Use a new patch directory; existing evidence is retained'}
foreach($packageRoot in @($baseRoot,$updatedRoot)){
    & (Join-Path $PSScriptRoot 'Test-CommissioningPackage.ps1') -PackagePath $packageRoot
}
$baseline=Get-Content -LiteralPath (Join-Path $baseRoot 'manifest.json') -Raw|ConvertFrom-Json
$updated=Get-Content -LiteralPath (Join-Path $updatedRoot 'manifest.json') -Raw|ConvertFrom-Json
$oldFiles=@{}; foreach($entry in $baseline.files){$oldFiles[$entry.path]=$entry}
$newFiles=@{}; foreach($entry in $updated.files){$newFiles[$entry.path]=$entry}
foreach($path in $oldFiles.Keys){if(-not $newFiles.ContainsKey($path)){throw ('Patch cannot remove base file: '+$path)}}
[IO.Directory]::CreateDirectory((Join-Path $patchRoot 'payload'))|Out-Null
$changes=@()
foreach($entry in $updated.files){
    $old=$oldFiles[$entry.path]
    if($null -ne $old -and $old.sha256 -eq $entry.sha256){continue}
    $destination=Join-Path $patchRoot ('payload/'+$entry.path)
    [IO.Directory]::CreateDirectory((Split-Path $destination))|Out-Null
    Copy-Item -LiteralPath (Join-Path $updatedRoot $entry.path) -Destination $destination
    $changes+=@{path=$entry.path;sha256=$entry.sha256;bytes=$entry.bytes;baseSha256=if($old){$old.sha256}else{$null}}
}
Copy-Item -LiteralPath (Join-Path $updatedRoot 'manifest.json') -Destination (Join-Path $patchRoot 'target-manifest.json')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Apply-CommissioningPatch.ps1') -Destination $patchRoot
@{schemaVersion='commissioning-patch/1';basePackageId=$baseline.packageId;targetPackageId=$updated.packageId;
    baseManifestSha256=(Get-FileHash -LiteralPath (Join-Path $baseRoot 'manifest.json')).Hash;
    targetManifestSha256=(Get-FileHash -LiteralPath (Join-Path $patchRoot 'target-manifest.json')).Hash;
    createdUtc=[DateTimeOffset]::UtcNow.ToString('o');files=$changes;fieldValidated=$false;hardwareConnected=$false;
    rollback='Retain base package and old independent installation; no in-place database migration or automatic action replay'
}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $patchRoot 'patch-manifest.json') -Encoding utf8
@'
This patch applies only to the exact final-2 base manifest recorded in patch-manifest.json.
Verify the ZIP SHA256 sidecar, extract to a new folder, then run:
  .\Apply-CommissioningPatch.ps1 -BasePackagePath 'D:\gaode\artifacts\gaode-commissioning-console-021-final-2' -Destination 'D:\gaode\artifacts\gaode-commissioning-console-021-final-3-patched'
The script validates base and payload hashes, creates a NEW package directory, and validates the result. It never starts hardware.
Install the resulting package using its scripts\Install-CommissioningConsole.ps1 into a NEW installation directory.
Never apply to a running installation or copy/replace an old data directory. Old in-flight or unknown actions are not resumed.
This update implements confirmed PLC sequencing; full runtime configuration and field acceptance remain separate.
'@|Set-Content -LiteralPath (Join-Path $patchRoot 'README.txt') -Encoding utf8
@{patch=$patchRoot;changedFiles=$changes.Count;payloadBytes=($changes|Measure-Object bytes -Sum).Sum}|ConvertTo-Json
