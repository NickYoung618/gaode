param([Parameter(Mandatory)][string]$BasePackagePath,
      [Parameter(Mandatory)][string]$Destination,
      [string]$PatchPath=$PSScriptRoot)
$ErrorActionPreference='Stop'
$baseRoot=[IO.Path]::GetFullPath($BasePackagePath)
$patchRoot=[IO.Path]::GetFullPath($PatchPath)
$targetRoot=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $targetRoot){throw 'Destination exists; base and old installation must remain unchanged'}
if(Test-Path -LiteralPath (Join-Path $baseRoot 'data')){throw 'Use the immutable base package, not a live installation'}
$patch=Get-Content -LiteralPath (Join-Path $patchRoot 'patch-manifest.json') -Raw|ConvertFrom-Json
if($patch.schemaVersion -ne 'commissioning-patch/1'){throw 'Unknown patch schema'}
if((Get-FileHash -LiteralPath (Join-Path $baseRoot 'manifest.json')).Hash -ne $patch.baseManifestSha256){throw 'Wrong base package manifest'}
if((Get-FileHash -LiteralPath (Join-Path $patchRoot 'target-manifest.json')).Hash -ne $patch.targetManifestSha256){throw 'Target manifest hash mismatch'}
$baseline=Get-Content -LiteralPath (Join-Path $baseRoot 'manifest.json') -Raw|ConvertFrom-Json
$updated=Get-Content -LiteralPath (Join-Path $patchRoot 'target-manifest.json') -Raw|ConvertFrom-Json
if($baseline.packageId -ne $patch.basePackageId -or $updated.packageId -ne $patch.targetPackageId){throw 'Package identity mismatch'}
function Resolve-Child([string]$root,[string]$relative){
    if([IO.Path]::IsPathRooted($relative)){throw 'Absolute patch path is invalid'}
    $resolved=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(-not $resolved.StartsWith($root.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escapes package'}
    return $resolved
}
function Check-Files([string]$root,$entries){
    foreach($entry in $entries){
        $path=Resolve-Child $root $entry.path
        if(-not(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -ne $entry.bytes -or
            (Get-FileHash -LiteralPath $path).Hash -ne $entry.sha256){throw ('File hash mismatch: '+$entry.path)}
    }
}
# Validate all baseline and payload files before creating the destination.
Check-Files $baseRoot $baseline.files
Check-Files (Join-Path $patchRoot 'payload') $patch.files
[IO.Directory]::CreateDirectory($targetRoot)|Out-Null
# Copy only manifest-listed immutable package files, never local data or secrets.
foreach($entry in $baseline.files){
    $destinationFile=Resolve-Child $targetRoot $entry.path
    [IO.Directory]::CreateDirectory((Split-Path $destinationFile))|Out-Null
    Copy-Item -LiteralPath (Resolve-Child $baseRoot $entry.path) -Destination $destinationFile
}
foreach($entry in $patch.files){
    $destinationFile=Resolve-Child $targetRoot $entry.path
    [IO.Directory]::CreateDirectory((Split-Path $destinationFile))|Out-Null
    Copy-Item -LiteralPath (Resolve-Child (Join-Path $patchRoot 'payload') $entry.path) -Destination $destinationFile -Force
}
Copy-Item -LiteralPath (Join-Path $patchRoot 'target-manifest.json') -Destination (Join-Path $targetRoot 'manifest.json')
Check-Files $targetRoot $updated.files
@{schemaVersion='commissioning-patch-application/1';packageId=$updated.packageId;destination=$targetRoot;
    verifiedFiles=@($updated.files).Count;patchedFiles=@($patch.files).Count;passed=$true;
    baseChanged=$false;hardwareConnected=$false;deviceDispatches=0}|ConvertTo-Json
