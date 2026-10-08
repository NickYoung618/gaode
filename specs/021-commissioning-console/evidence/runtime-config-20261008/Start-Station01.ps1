param([switch]$Run)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
if ($Run) {
    if(Get-NetTCPConnection -LocalPort 5190 -State Listen -ErrorAction SilentlyContinue) {
        throw '5190端口已被占用。请核对已有程序，不要重复启动。'
    }
    & (Join-Path $root 'scripts/Set-CommissioningIdentityEnvironment.ps1') -InstallationRoot $root -Role operator
    & (Join-Path $root 'scripts/Start-CommissioningConsole.ps1') -Run
} else {
    & (Join-Path $root 'scripts/Start-CommissioningConsole.ps1') -CheckOnly
}
