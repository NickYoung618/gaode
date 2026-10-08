param([Parameter(Mandatory)][string]$OutputPath,
    [string]$RecipeSourceRoot='D:\gaode\specs\021-commissioning-console\evidence\local-recipe-conversion-20261008',
    [string]$GalaxyWrapperPath='C:\Users\Administrator\Desktop\相机接入调试\commissioning-0.1.2-work-20261003-141852\source\vendor\GxIAPINET.dll',
    [string]$CameraProIncludeRoot='D:\软件开发sdk\3DCameraViewer\CamSDK\CamSDK_CSharp\include')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$target=[IO.Path]::GetFullPath($OutputPath)
if(Test-Path -LiteralPath $target){throw 'Use a new package directory; existing files will not be overwritten.'}
foreach($path in @($RecipeSourceRoot,$GalaxyWrapperPath,$CameraProIncludeRoot)){
    if(-not(Test-Path -LiteralPath $path)){throw ('Missing build input: '+$path)}
}
[IO.Directory]::CreateDirectory($target)|Out-Null
Push-Location $repo
try {
    foreach($entry in @(
        @('backend/src/Gaode.Host/Gaode.Host.csproj','host'),
        @('desktop/Gaode.Station01.Desktop.csproj','desktop'),
        @('backend/src/Gaode.CameraWorker/Gaode.CameraWorker.csproj','worker'),
        @('backend/tools/Gaode.DeploymentPrep/Gaode.DeploymentPrep.csproj','prep'))){
        & dotnet publish $entry[0] -c Release --self-contained false -o (Join-Path $target ('app/'+$entry[1])) "-p:GalaxyWrapperPath=$GalaxyWrapperPath" "-p:CameraProIncludeRoot=$CameraProIncludeRoot" -p:RestoreLockedMode=true -v minimal
        if($LASTEXITCODE){throw ('Publish failed: '+$entry[0])}
    }
    foreach($dir in @('config','scripts','evidence','source','notices')){
        [IO.Directory]::CreateDirectory((Join-Path $target $dir))|Out-Null
    }
    Copy-Item -LiteralPath (Join-Path $repo 'frontend/dist') -Destination (Join-Path $target 'app/frontend') -Recurse
    foreach($name in @('Install-CommissioningConsole.ps1','Start-CommissioningConsole.ps1','Wait-CommissioningIdentity.ps1','Stop-CommissioningConsole.ps1','Reset-CommissioningConsole.ps1','Prepare-CommissioningInputs.ps1','Test-CommissioningPackage.ps1','Prepare-CommissioningIdentity.ps1','Set-CommissioningIdentityEnvironment.ps1','Apply-CommissioningPatch.ps1')){
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $target 'scripts')
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Stop-CommissioningConsole.cmd') -Destination (Join-Path $target '一键关闭.cmd')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Reset-CommissioningConsole.cmd') -Destination (Join-Path $target '一键复位.cmd')
    foreach($name in @('recipe-source.json','recipe-source-map.json','original-recipe.local.json')){
        Copy-Item -LiteralPath (Join-Path $RecipeSourceRoot $name) -Destination (Join-Path $target 'config')
    }
    Copy-Item -LiteralPath (Join-Path $repo 'configuration/plc/confirmed-20261006') -Destination (Join-Path $target 'config/plc') -Recurse
    Copy-Item -LiteralPath (Join-Path $repo 'specs/001-station01-public-preparation/contracts') -Destination (Join-Path $target 'config/schemas') -Recurse
    Copy-Item -LiteralPath (Join-Path $repo 'specs/021-commissioning-console/contracts/public-config.runtime.schema.json') -Destination (Join-Path $target 'config/schemas/public-config.runtime.schema.json')
    $snapshot=Get-Content -LiteralPath (Join-Path $repo 'artifacts/camera-runtime/seven-after-captures.json') -Raw|ConvertFrom-Json
    @{SchemaVersion=1;Cameras=@($snapshot.cameras|ForEach-Object{
        @{Role=$_.role;Kind=if($_.role -eq '3D'){'3D'}else{'2D'};Serial=$_.serial;ExpectedNicMac=$_.expectedNicMac;Note='来自本机既有七机采集记录；本次未重新连接设备'}
    })}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $target 'config/camera.site.json') -Encoding utf8
    @{schemaVersion='commissioning-runtime-profile/1';fieldConfigurationReviewed=$false;sourceReference='';apiBaseUrl='http://127.0.0.1:5190';hostSettings=$null;desktopProfile=$null;
        remaining=@('完整公共配置/行程及预算装配','在PLC机械配置中加载已确认siteOperations','本安装身份与桌面配置','核对本安装配方版本引用')
    }|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $target 'config/runtime-profile.template.json') -Encoding utf8
    foreach($name in @('validation.md','tasks.md','quickstart.md')){
        Copy-Item -LiteralPath (Join-Path $repo ('specs/021-commissioning-console/'+$name)) -Destination (Join-Path $target 'evidence')
    }
    foreach($name in @('commissioning-regression.trx','F-position.trx','light-and-query-increment.trx','deployment-final.trx','two-round-final-r4.trx')){
        Copy-Item -LiteralPath (Join-Path $repo ('specs/021-commissioning-console/evidence/test-results/'+$name)) -Destination (Join-Path $target 'evidence')
    }
    Copy-Item -LiteralPath (Join-Path $repo 'specs/021-commissioning-console/evidence/console-20261007-222550') -Destination (Join-Path $target 'evidence/desktop-console') -Recurse
    Copy-Item -LiteralPath (Join-Path $repo 'specs/021-commissioning-console/evidence/two-round-final-20261008-r4/two-round-final-completion.json') -Destination (Join-Path $target 'evidence')
    Copy-Item -LiteralPath (Join-Path $repo 'specs/021-commissioning-console/evidence/deployment-regression-20261008-final/imported-recipe-execution.json') -Destination (Join-Path $target 'evidence')
    Copy-Item -LiteralPath (Join-Path $repo 'specs/021-commissioning-console/evidence/deployment-final-20261008/field-limits-user-r1.json') -Destination (Join-Path $target 'config/field-limits-source-pending-review.json')
    foreach($name in @('site-operations-confirmed-20261008.json','field-limits-confirmed-20261008.json')){
        Copy-Item -LiteralPath (Join-Path $repo ('configuration/commissioning/'+$name)) -Destination (Join-Path $target 'config')
    }
    Copy-Item -LiteralPath (Join-Path $repo 'specs/021-commissioning-console/evidence/test-results/plc-sequence-final.trx') -Destination (Join-Path $target 'evidence')
    Copy-Item -LiteralPath (Join-Path $repo 'specs/021-commissioning-console/evidence/plc-sequence-final-20261008') -Destination (Join-Path $target 'evidence/plc-sequence') -Recurse
    Copy-Item -LiteralPath (Join-Path $repo 'specs/021-commissioning-console/evidence/prototype-F-position-final-20261008.json') -Destination (Join-Path $target 'evidence')
    foreach($notice in @(
        'C:\Users\Administrator\Desktop\相机接入调试\commissioning-0.1.2-work-20261003-141852\app\third-party-notices\galaxy-10400682-license.rtf',
        'D:\GalaxySDK\License\galaxy_3rd_party_licenses.txt')){
        Copy-Item -LiteralPath $notice -Destination (Join-Path $target 'notices')
    }
    & (Join-Path $PSScriptRoot 'Freeze-CommissioningPackage.ps1') -PackagePath $target
    if($LASTEXITCODE){throw 'Package metadata freeze failed'}
} finally {Pop-Location}
Write-Output ('Package built; no device connection: '+$target)
