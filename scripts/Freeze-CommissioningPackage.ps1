param([Parameter(Mandatory)][string]$PackagePath)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$target=[IO.Path]::GetFullPath($PackagePath)
Copy-Item -LiteralPath (Join-Path $repo 'specs/021-commissioning-console/deployment-readme.md') -Destination (Join-Path $target 'README.md')
$entries=@()
foreach($relative in @(& git -C $repo ls-files --cached --others --exclude-standard)){
    if($relative -notmatch '^(backend/(src|tools|tests)/|VirtualPlc/|desktop/|frontend/(src/|scripts/|tests/|package|tsconfig)|scripts/|configuration/|AGENTS.md$|global.json$|\.gitignore$|Directory\.|NuGet\.|.*\.slnx?$|\.specify/(feature.json|memory/)|specs/)'){continue}
    if($relative -match '(^|/)(bin|obj|evidence|node_modules|\.git)/' -or $relative -match 'appsettings.*\.json$'){continue}
    $input=Join-Path $repo $relative
    if(-not(Test-Path -LiteralPath $input -PathType Leaf)){continue}
    $copy=Join-Path $target ('source/'+$relative)
    [IO.Directory]::CreateDirectory((Split-Path $copy))|Out-Null
    Copy-Item -LiteralPath $input -Destination $copy
    $entries+=@{path=$relative;sha256=(Get-FileHash -LiteralPath $input -Algorithm SHA256).Hash;bytes=(Get-Item -LiteralPath $input).Length}
}
$entries|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $target 'source/source-files.json') -Encoding utf8
$manifest=@{
    schemaVersion='commissioning-package/1';packageId=(Split-Path $target -Leaf);builtUtc=[DateTimeOffset]::UtcNow
    branch=(& git -C $repo branch --show-current);sourceHead=(& git -C $repo rev-parse HEAD);dirtySource=$true
    sourceFilesSha256=(Get-FileHash -LiteralPath (Join-Path $target 'source/source-files.json')).Hash
    framework='.NET 10 Windows x64, framework-dependent';algorithm='Simulated:scoped-versioned-inputs';externalLight='Simulated:explicit-skip'
    plc='Real:field-configuration-required';cameras='Real:seven-role-workers';nativeSdkBundled=$false;fieldValidated=$false
    state='BuiltPendingFieldConfiguration';rollback='Independent directory; old installation and tags unchanged'
    files=@(Get-ChildItem -LiteralPath $target -File -Recurse|Where-Object Name -ne 'manifest.json'|ForEach-Object{
        @{path=[IO.Path]::GetRelativePath($target,$_.FullName);bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
}
$manifest|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $target 'manifest.json') -Encoding utf8
