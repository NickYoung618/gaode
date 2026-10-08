$ErrorActionPreference='Stop'
$repo='D:\gaode'
. (Join-Path $repo 'scripts/Wait-CommissioningIdentity.ps1')
$profile=Join-Path $PSScriptRoot 'test-profile.json'
@{credentialEnvironmentVariable='GAODE_STARTUP_TEST_CREDENTIAL';profileId='offline-test';expectedSubjectId='offline:test';expectedRole='Operator'}|ConvertTo-Json|Set-Content -LiteralPath $profile
[Environment]::SetEnvironmentVariable('GAODE_STARTUP_TEST_CREDENTIAL','OFFLINE-NOT-A-REAL-CREDENTIAL','Process')
$script:calls=0;$script:wrong=$false
function Invoke-RestMethod {
    param($Uri,$Headers,[switch]$NoProxy,$TimeoutSec,$ErrorAction)
    if(-not $NoProxy -or $Uri -ne 'http://127.0.0.1:5190/api/v1/station01/identity'){throw 'Incorrect readiness request'}
    $script:calls++
    if($script:calls -lt 3){throw 'OFFLINE: listener not ready'}
    [pscustomobject]@{schemaVersion='station01-identity/1';mode='RealDeviceCommissioning';purpose='Commissioning';authenticationSource='PreconfiguredCommissioning';profileId='offline-test';subjectId=if($script:wrong){'wrong'}else{'offline:test'};role='Operator'}
}
try{
    Wait-CommissioningIdentity -ApiBaseUrl http://127.0.0.1:5190 -DesktopProfile $profile -HostProcessId $PID -TimeoutSeconds 3
    if($script:calls -ne 3){throw 'Readiness not observed'}
    $script:wrong=$true;$rejected=$false
    try{Wait-CommissioningIdentity -ApiBaseUrl http://127.0.0.1:5190 -DesktopProfile $profile -HostProcessId $PID -TimeoutSeconds 1}catch{$rejected=$_.Exception.Message -like '*身份与桌面配置不符*'}
    if(-not $rejected){throw 'Identity mismatch not rejected'}
    $exitRejected=$false
    try{Wait-CommissioningIdentity -ApiBaseUrl http://127.0.0.1:5190 -DesktopProfile $profile -HostProcessId 2147483647 -TimeoutSeconds 1}catch{$exitRejected=$_.Exception.Message -like '*后台已退出*'}
    if(-not $exitRejected){throw 'Exited host not rejected'}
    $timeoutRejected=$false
    try{Wait-CommissioningIdentity -ApiBaseUrl http://127.0.0.1:5190 -DesktopProfile $profile -HostProcessId $PID -TimeoutSeconds 0}catch{$timeoutRejected=$_.Exception.Message -like '*接口超时*'}
    if(-not $timeoutRejected){throw 'Timeout not rejected'}
    $scriptText=Get-Content -LiteralPath (Join-Path $repo 'scripts/Start-CommissioningConsole.ps1') -Raw
    if($scriptText.IndexOf('Wait-CommissioningIdentity -ApiBaseUrl') -gt $scriptText.IndexOf('Start-Process -FilePath $desktopExe')){throw 'Desktop precedes identity readiness'}
    @{passed=$true;checks=5;delayedIdentityPassed=$true;identityMismatchBlocked=$true;hostExitBlocked=$true;timeoutBlocked=$true;desktopFollowsReadiness=$true;transport='Mock-only';networkAccess=$false;hostStarted=$false;workersStarted=$false;deviceDispatches=0}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'startup-check.json')
}finally{
    [Environment]::SetEnvironmentVariable('GAODE_STARTUP_TEST_CREDENTIAL',$null,'Process')
}
Get-Content -LiteralPath (Join-Path $PSScriptRoot 'startup-check.json')
