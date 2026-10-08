param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$package=Split-Path $PSScriptRoot
& (Join-Path $PSScriptRoot 'Test-CommissioningPackage.ps1') -PackagePath $package
$target=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $target){throw '安装目录已存在，请选择新的版本目录。旧安装不会被覆盖。'}
[IO.Directory]::CreateDirectory($target)|Out-Null
foreach($name in @('app','config','scripts','evidence','source','notices','README.md','manifest.json','一键关闭.cmd','一键复位.cmd')){
    $item=Join-Path $package $name
    if(Test-Path -LiteralPath $item){Copy-Item -LiteralPath $item -Destination $target -Recurse}
}
$data=Join-Path $target 'data'
[IO.Directory]::CreateDirectory($data)|Out-Null
$prep=Join-Path $target 'app/prep/Gaode.DeploymentPrep.exe'
& $prep --prepare-runtime $data (Join-Path $data 'store') RealDeviceCommissioning
if($LASTEXITCODE){throw '运行库准备失败，未连接设备。'}
$recipes=Join-Path $data 'recipes'
& $prep --prepare-recipes $data $recipes
if($LASTEXITCODE){throw '配方库准备失败，未连接设备。'}
$source=Join-Path $target 'config/recipe-source.json'
$digest=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
& $prep --seed-authoring $recipes $source $digest
if($LASTEXITCODE){throw '正式配方校验/录入失败；没有修改原联调配方。'}
& $prep --inspect-recipes $recipes (Join-Path $data 'recipe-readback.json')
if($LASTEXITCODE){throw '正式配方重读/规划失败。'}
& (Join-Path $target 'scripts/Prepare-CommissioningInputs.ps1') -InstallationRoot $target
$inputPath=Join-Path $data 'config/commissioning.json'
& $prep --inspect-commissioning $inputPath (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash
if($LASTEXITCODE){throw '虚拟算法输入版本/作用范围校验失败。'}
$report=@{schemaVersion='commissioning-install/1';installedUtc=[DateTimeOffset]::UtcNow;installation=$target;packageId=(Get-Content (Join-Path $package 'manifest.json') -Raw|ConvertFrom-Json).packageId;recipeSourceSha256=$digest;state='InstalledPendingFieldConfiguration';hardwareConnected=$false;hardwareDispatches=0;previousInstallationChanged=$false}
$report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $data 'installation.json') -Encoding utf8
& (Join-Path $target 'scripts/Start-CommissioningConsole.ps1') -CheckOnly
Write-Output ('安装完成；配方已保存并重读。现场配置未核定，不启动设备：'+$target)
