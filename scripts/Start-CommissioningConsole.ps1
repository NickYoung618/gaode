param([switch]$CheckOnly,[switch]$Run)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$profilePath=Join-Path $root 'config/runtime-profile.json'
$ready=Test-Path -LiteralPath $profilePath
$reasons=@()
if(-not $ready){$reasons+= 'FieldRuntimeProfileMissing'}
else{
    $profile=Get-Content -LiteralPath $profilePath -Raw|ConvertFrom-Json
    foreach($file in @($profile.hostSettings,$profile.desktopProfile)){
        if(-not $file -or -not [IO.Path]::IsPathFullyQualified($file) -or -not(Test-Path -LiteralPath $file)){$reasons+='RuntimeSettingsMissing'}
    }
    if(-not $profile.fieldConfigurationReviewed -or -not $profile.sourceReference){$reasons+='FieldConfigurationNotReviewed'}
}
$check=@{schemaVersion='commissioning-launch-check/1';packageRoot=$root;networkAccess=$false;deviceDispatches=0;state=if($reasons.Count){'ConfigurationRequired'}else{'ConfiguredNotFieldValidated'};reasons=$reasons;recipeReadbackPresent=(Test-Path -LiteralPath (Join-Path $root 'data/recipe-readback.json'))}
if(Test-Path -LiteralPath (Join-Path $root 'data')){$check|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $root 'data/last-launch-check.json') -Encoding utf8}
$check|ConvertTo-Json -Depth 5|Write-Output
# Default is a local file check. Physical connections require an explicit -Run and complete reviewed configuration.
if($CheckOnly -or -not $Run){return}
if($reasons.Count){throw '尚缺已核定现场配置；未启动PLC或相机。详见README与配方映射。'}
$hostExe=Join-Path $root 'app/host/Gaode.Host.exe';$desktopExe=Join-Path $root 'app/desktop/Gaode.Station01.Desktop.exe'
$env:GAODE_MODE='RealDeviceCommissioning';$env:GAODE_FRONTEND_DIST=Join-Path $root 'app/frontend'
$env:GAODE_COMMISSIONING_PROFILE_PATH=$profile.desktopProfile
$env:GAODE_API_BASE_URL=$profile.apiBaseUrl;$env:GAODE_SIGNALR_URL=$profile.apiBaseUrl+'/hubs/station01'
$settings=Get-Content -LiteralPath $profile.hostSettings -Raw|ConvertFrom-Json
# Use the existing ASP.NET environment provider; never print secret values.
function Set-SettingsEnvironment($value,[string]$prefix) {
    if($value -is [PSCustomObject]) {
        foreach($property in $value.PSObject.Properties){
            $key=if($prefix){$prefix+'__'+$property.Name}else{$property.Name}
            Set-SettingsEnvironment $property.Value $key
        }
        return
    }
    if($value -is [System.Collections.IList]) {
        for($i=0;$i -lt $value.Count;$i++){Set-SettingsEnvironment $value[$i] ($prefix+'__'+$i)}
        return
    }
    if($null -ne $value){[Environment]::SetEnvironmentVariable($prefix,[string]$value,'Process')}
}
Set-SettingsEnvironment $settings ''
# Preserve the previous Host's failure evidence before Start-Process truncates
# the fixed current-log paths. A failed copy must prevent that truncation.
$previousLogs=@('host.stdout.log','host.stderr.log' | ForEach-Object { Join-Path $root ('data/'+$_) } | Where-Object { Test-Path -LiteralPath $_ })
if($previousLogs.Count){
    $archiveName=[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmssfff')+'-'+[Guid]::NewGuid().ToString('N')
    $archivePath=Join-Path $root ('data/host-log-history/'+$archiveName)
    [IO.Directory]::CreateDirectory($archivePath)|Out-Null
    foreach($previousLog in $previousLogs){Copy-Item -LiteralPath $previousLog -Destination $archivePath -ErrorAction Stop}
    Write-Output ('上次后台日志已保留：'+$archivePath)
}
$hostProcess=Start-Process -FilePath $hostExe -WorkingDirectory (Join-Path $root 'app/host') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $root 'data/host.stdout.log') -RedirectStandardError (Join-Path $root 'data/host.stderr.log')
$hostProcess.Id|Set-Content -LiteralPath (Join-Path $root 'data/host.pid')
Write-Output ('Host启动 PID='+$hostProcess.Id+'；等待后台身份接口就绪。')
. (Join-Path $PSScriptRoot 'Wait-CommissioningIdentity.ps1')
Wait-CommissioningIdentity -ApiBaseUrl $profile.apiBaseUrl -DesktopProfile $profile.desktopProfile -HostProcessId $hostProcess.Id
Write-Output '后台身份已核对，打开桌面；设备是否就绪请查看页面状态。'
# Desktop has no device connection itself. Leave credentials in the maintenance-configured environment.
Start-Process -FilePath $desktopExe -WorkingDirectory (Join-Path $root 'app/desktop') -WindowStyle Hidden|Out-Null
