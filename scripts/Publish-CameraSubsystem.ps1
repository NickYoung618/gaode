param(
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$GalaxyWrapperPath = 'C:\Users\Administrator\Desktop\相机接入调试\commissioning-0.1.2-work-20261003-141852\source\vendor\GxIAPINET.dll',
    [string]$CameraProIncludeRoot = 'D:\软件开发sdk\3DCameraViewer\CamSDK\CamSDK_CSharp\include'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$target = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $target) { throw 'Use a new version directory; existing package is never overwritten.' }
if (!(Test-Path -LiteralPath $GalaxyWrapperPath) -or !(Test-Path -LiteralPath $CameraProIncludeRoot)) { throw 'Vendor build dependencies missing.' }
New-Item -ItemType Directory -Path $target | Out-Null
Push-Location $repo
try {
    foreach ($project in @('Gaode.Host','Gaode.CameraWorker')) {
        $sub = if ($project -eq 'Gaode.Host') { 'host' } else { 'worker' }
        & dotnet publish "backend/src/$project/$project.csproj" -c Release -r win-x64 --self-contained false -o (Join-Path $target $sub) "-p:GalaxyWrapperPath=$GalaxyWrapperPath" "-p:CameraProIncludeRoot=$CameraProIncludeRoot" -p:RestoreLockedMode=true -v minimal
        if ($LASTEXITCODE) { throw "Publish failed: $project" }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-CameraSubsystem.ps1'),(Join-Path $PSScriptRoot 'Invoke-CameraAcceptance.ps1') -Destination $target
    Copy-Item -LiteralPath (Join-Path $repo 'specs/019-real-camera-subsystem/quickstart.md') -Destination (Join-Path $target 'README.md')
    foreach ($document in @('validation.md','research.md')) {
        Copy-Item -LiteralPath (Join-Path $repo "specs/019-real-camera-subsystem/$document") -Destination $target
    }
    $notice = 'C:\Users\Administrator\Desktop\相机接入调试\commissioning-0.1.2-work-20261003-141852\app\third-party-notices\galaxy-10400682-license.rtf'
    if (!(Test-Path -LiteralPath $notice)) { throw 'Galaxy redistribution notice missing.' }
    New-Item -ItemType Directory -Path (Join-Path $target 'notices') | Out-Null
    Copy-Item -LiteralPath $notice,'D:\GalaxySDK\License\galaxy_3rd_party_licenses.txt' -Destination (Join-Path $target 'notices')
    $revision = & git rev-parse HEAD
    $entries = Get-ChildItem -LiteralPath $target -Recurse -File | ForEach-Object {
        @{ path = [IO.Path]::GetRelativePath($target,$_.FullName); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    @{ builtUtc=[DateTimeOffset]::UtcNow; sourceRevision=$revision; framework='Microsoft.AspNetCore.App 10.0 / Windows x64'; nativeSdkBundled=$false; files=@($entries) } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $target 'manifest.json') -Encoding utf8
} finally { Pop-Location }
Write-Output $target
