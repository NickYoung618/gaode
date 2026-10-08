param([Parameter(Mandatory)][string]$PackagePath)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($PackagePath)
$manifest=Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw|ConvertFrom-Json
if($manifest.schemaVersion -ne 'commissioning-package/1'){throw 'Unknown package manifest'}
$prefix=$root.TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
foreach($entry in $manifest.files){
    $path=[IO.Path]::GetFullPath((Join-Path $root $entry.path))
    if(-not $path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest path outside package'}
    if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw ('Missing package file: '+$entry.path)}
    if((Get-Item -LiteralPath $path).Length -ne $entry.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256){throw ('Package hash mismatch: '+$entry.path)}
}
@{schemaVersion='commissioning-package-check/1';packageId=$manifest.packageId;verifiedFiles=@($manifest.files).Count;passed=$true;hardwareConnected=$false}|ConvertTo-Json
