function Wait-CommissioningIdentity {
    param([Parameter(Mandatory)][string]$ApiBaseUrl,
          [Parameter(Mandatory)][string]$DesktopProfile,
          [Parameter(Mandatory)][int]$HostProcessId,
          [int]$TimeoutSeconds=90)
    $uri=[Uri]($ApiBaseUrl.TrimEnd('/')+'/api/v1/station01/identity')
    if(-not $uri.IsLoopback -or $uri.Scheme -ne 'http'){throw '启动身份检查仅允许本机HTTP接口。'}
    $profile=Get-Content -LiteralPath $DesktopProfile -Raw|ConvertFrom-Json
    $credential=[Environment]::GetEnvironmentVariable($profile.credentialEnvironmentVariable,'Process')
    if(-not $credential){throw '未加载本机联调身份；未打开桌面。'}
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds){
        $process=Get-Process -Id $HostProcessId -ErrorAction SilentlyContinue
        if(-not $process -or $process.HasExited){throw '后台已退出；未打开桌面，请检查data/host.stdout.log和host.stderr.log。'}
        $identity=$null
        try{
            $identity=Invoke-RestMethod -Uri $uri.AbsoluteUri -Headers @{Authorization='Bearer '+$credential} -NoProxy -TimeoutSec 2 -ErrorAction Stop
        }catch{
            $status=$_.Exception.Response.StatusCode
            if($status -and [int]$status -in @(401,403)){throw '后台拒绝已配置身份；未打开桌面。'}
        }
        if($null -ne $identity){
            if($identity.schemaVersion -ne 'station01-identity/1' -or
               $identity.mode -ne 'RealDeviceCommissioning' -or $identity.purpose -ne 'Commissioning' -or
               $identity.authenticationSource -ne 'PreconfiguredCommissioning' -or
               $identity.profileId -ne $profile.profileId -or $identity.subjectId -ne $profile.expectedSubjectId -or
               $identity.role -ne $profile.expectedRole){throw '后台身份与桌面配置不符；未打开桌面。'}
            return
        }
        Start-Sleep -Milliseconds 200
    }
    throw '等待后台身份接口超时；未打开桌面，请检查启动日志。'
}
