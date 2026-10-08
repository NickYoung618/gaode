$ErrorActionPreference = 'Stop'
$plcUrl = 'http://127.0.0.1:18770'
$plcState = $null
try { $plcState = Invoke-RestMethod -Uri "$plcUrl/api/state" -TimeoutSec 2 } catch {}
if (-not $plcState) { Write-Host '本独立联调服务未运行。'; exit 0 }
if ($plcState.app -ne 'Gaode Independent PLC Commissioning' -or $plcState.rootPath -ne $PSScriptRoot) { throw '该端口不是本目录服务，未停止。' }
$plcSession = Invoke-RestMethod -Uri "$plcUrl/api/session" -TimeoutSec 2
Invoke-RestMethod -Uri "$plcUrl/api/shutdown" -Method Post -ContentType 'application/json' -Headers @{'X-Console-Token'=$plcSession.token} -Body '{}' -TimeoutSec 10 | Out-Null
Write-Host '本目录后台已停止；停止通讯不等于停止设备。'
