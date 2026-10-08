param(
    [Parameter(Mandatory)][string]$SitePath,
    [Parameter(Mandatory)][string]$DataRoot,
    [string]$GalaxySdkPath = 'D:\GalaxySDK\APIDll\Win64',
    [string]$CameraProSdkPath = 'D:\软件开发sdk\3DCameraViewer\CamSDK\CamSDK_CSharp\bin',
    [int]$Port = 5189,
    [switch]$PrepareStore
)
$ErrorActionPreference = 'Stop'
$hostExe = Join-Path $PSScriptRoot 'host/Gaode.Host.exe'
$workerExe = Join-Path $PSScriptRoot 'worker/Gaode.CameraWorker.exe'
foreach ($path in @($SitePath,$DataRoot,$GalaxySdkPath,$CameraProSdkPath)) {
    if (![IO.Path]::IsPathFullyQualified($path)) { throw 'All configured paths must be absolute.' }
}
foreach ($path in @($SitePath,$hostExe,$workerExe,(Join-Path $GalaxySdkPath 'GxIAPI.dll'),(Join-Path $CameraProSdkPath 'CameraPro.dll'))) {
    if (!(Test-Path -LiteralPath $path)) { throw "Missing dependency: $path" }
}
New-Item -ItemType Directory -Path $DataRoot -Force | Out-Null
$store = Join-Path $DataRoot 'store'
if ($PrepareStore) {
    if (Test-Path -LiteralPath (Join-Path $store 'camera.db')) { throw 'Store exists; do not prepare again.' }
    & $hostExe --prepare-camera-store $store
    if ($LASTEXITCODE) { throw 'Store preparation failed.' }
}
if (!(Test-Path -LiteralPath (Join-Path $store 'camera.db'))) { throw 'First start requires -PrepareStore.' }
$configPath = Join-Path $DataRoot 'camera.settings.json'
if (!(Test-Path -LiteralPath $configPath)) {
    $operatorToken = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $adminToken = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    @{ Urls="http://127.0.0.1:$Port"; Gaode=@{ Mode='Production'; CaptureOnly=$true; CameraStoreRoot=$store; Tokens=@{Operator=$operatorToken;SystemAdministrator=$adminToken}; Cameras=@{ SitePath=$SitePath;WorkerPath=$workerExe;GalaxySdkPath=$GalaxySdkPath;CameraProSdkPath=$CameraProSdkPath;StateRoot=(Join-Path $DataRoot 'state') } } } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $configPath -Encoding utf8
    @{Authorization="Bearer $operatorToken"} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $DataRoot 'operator.headers.json') -Encoding utf8
    @{Authorization="Bearer $adminToken"} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $DataRoot 'admin.headers.json') -Encoding utf8
}
# Existing config/tokens remain stable across restart; review paths when changing package version.
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$config.Gaode.Cameras.WorkerPath = $workerExe
$config.Gaode.Cameras.SitePath = $SitePath
$config | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'host/appsettings.json') -Encoding utf8
$process = Start-Process -FilePath $hostExe -WorkingDirectory (Join-Path $PSScriptRoot 'host') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $DataRoot 'host.stdout.log') -RedirectStandardError (Join-Path $DataRoot 'host.stderr.log')
$process.Id | Set-Content -LiteralPath (Join-Path $DataRoot 'host.pid')
Write-Output "Host PID $($process.Id); settings/headers/logs in $DataRoot. Check GET /api/v1/cameras before capturing."
