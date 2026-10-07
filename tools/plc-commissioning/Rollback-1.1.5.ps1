param([switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$plcReleaseRoot = 'D:\Gaode-PlcCommissioning-20261006\release'
$plcOldRoot = Join-Path $plcReleaseRoot 'Gaode-PlcCommissioning-1.1.5-win-x64'
$plcNewRoot = Join-Path $plcReleaseRoot 'Gaode-PlcCommissioning-1.1.6-win-x64'
if (-not (Test-Path -LiteralPath (Join-Path $plcOldRoot 'runtime\python.exe'))) { throw '缺少1.1.5完整包，未回退。' }
$plcState = $null
try { $plcState = Invoke-RestMethod 'http://127.0.0.1:18770/api/state' -TimeoutSec 2 } catch {}
if ($plcState) {
    if ($plcState.app -ne 'Gaode Independent PLC Commissioning' -or $plcState.rootPath -notin @($plcOldRoot,$plcNewRoot)) { throw '端口不是本次两个版本的服务，未回退。' }
    $plcRun = $plcState.recipe.run
    $plcMoving = @($plcState.actions | Where-Object { $_.state -in @('observing','pending') }).Count -gt 0
    if ($plcState.tcpConnected -or $plcMoving -or ($plcRun -and $plcRun.state -in @('running','waiting','paused'))) { throw '请先结束本盘、处理在途动作并在页面断开PLC，再执行回退；未停止现有服务。' }
    if ($plcState.rootPath -eq $plcNewRoot) {
        & (Join-Path $plcNewRoot 'Stop-PLC.ps1')
        for ($plcAttempt=0; $plcAttempt -lt 50; $plcAttempt++) {
            Start-Sleep -Milliseconds 100
            try { $null=Invoke-RestMethod 'http://127.0.0.1:18770/api/state' -TimeoutSec 1 } catch { break }
        }
        if ($plcAttempt -eq 50) { throw '新版服务未退出，未启动旧版。' }
    }
}
$plcMarker=Join-Path $plcNewRoot 'release.ready.json'
if (Test-Path -LiteralPath $plcMarker) {
    $plcReady=Get-Content -LiteralPath $plcMarker -Raw | ConvertFrom-Json
    if ($plcReady.version -ne '1.1.6') { throw '新版标记不符，未回退。' }
    $plcReady.ready=$false
    $plcReady | ConvertTo-Json | Set-Content -LiteralPath $plcMarker -Encoding UTF8
}
& (Join-Path $plcOldRoot 'Start-PLC.ps1') -View manual -NoBrowser:$NoBrowser
Write-Host '已回退1.1.5，工作区两个启动入口也会选1.1.5。PLC未连接，请在页面核对旧版配置。'
