#requires -Version 7.4
param([string]$Case='Q01', [switch]$Headless,
    [int]$ApiPort=5001, [int]$PlcApiPort=5080, [int]$PlcPort=1502)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
if (-not $Headless -and [Diagnostics.Process]::GetCurrentProcess().SessionId -eq 0) { throw '请在已登录的 Windows 桌面双击 Start.cmd。' }
foreach ($name in 'dotnet','python') { Get-Command $name -ErrorAction Stop | Out-Null }
$runtimes=(& dotnet --list-runtimes | Out-String)
foreach ($framework in 'Microsoft.NETCore.App','Microsoft.AspNetCore.App','Microsoft.WindowsDesktop.App') {
    if ($runtimes -notmatch ([regex]::Escape($framework)+' 10\.0\.')) { throw "缺少 $framework 10.0 x64 运行时。" }
}
$cases=Get-Content (Join-Path $root 'cases.json') -Raw | ConvertFrom-Json -AsHashtable
if (-not $cases.ContainsKey($Case)) { throw "可选 Case：$($cases.Keys -join ', ')" }
$base=Join-Path $root 'artifacts/recipe-execution-008'
$marker=Join-Path $base 'local-active.json'
if ((Test-Path $marker) -and (Get-Content $marker -Raw | ConvertFrom-Json).state -eq 'Active') { throw '请先双击 Stop.cmd 停止本包上次会话。' }
foreach ($port in @($ApiPort,$PlcApiPort,$PlcPort)) {
    if ([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() | Where-Object Port -eq $port) { throw "端口 $port 已使用；请停止你自己的旧测试或通过参数选择其他端口。" }
}
$fixture=Get-Content (Join-Path $root $cases[$Case]) -Raw | ConvertFrom-Json -AsHashtable
foreach ($key in 'configRoot','recipeCatalogPath','imageManifestPath','workerManifestPath','workerScriptPath') { $fixture[$key]=[IO.Path]::GetFullPath((Join-Path $root $fixture[$key])) }
$id=(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8)
$run=Join-Path $base "local-$Case-$id"
New-Item -ItemType Directory -Force $base | Out-Null
$prepared=Join-Path $base "fixture-$id.json"
$fixture | ConvertTo-Json -Depth 20 | Set-Content $prepared -Encoding utf8
$api="http://127.0.0.1:$ApiPort"
$plcApi="http://127.0.0.1:$PlcApiPort"
$token=[guid]::NewGuid().ToString('N')
$hostDll=Join-Path $root 'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll'
$plcDll=Join-Path $root 'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll'
$desktopExe=Join-Path $root 'desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe'
$recordPath=Join-Path $run 'process.json'
try {
    $env:Simulation__ScanPeriodMs='20'
    & (Join-Path $root 'scripts/start-station01-virtual-loop.ps1') -OperatorToken $token -TestRoot $run -FixtureManifest $prepared -HostDll $hostDll -PlcDll $plcDll -ApiBase $api -PlcApiBase $plcApi -PlcPort $PlcPort -SkipDesktop -WindowsNativeThreadPool | Out-Null
    $record=Get-Content $recordPath -Raw | ConvertFrom-Json -AsHashtable
    $record.ownedProcessStartUtc=@{}
    foreach ($key in 'hostPid','plcPid','workerPid') {
        if (-not $record[$key]) { throw "缺少 $key；检查 $recordPath。" }
        $process=Get-CimInstance Win32_Process -Filter "ProcessId = $($record[$key])"
        if (-not $process) { throw "$key 已退出。" }
        $record.ownedProcessStartUtc[$key]=$process.CreationDate.ToUniversalTime().ToString('o')
    }
    $record | ConvertTo-Json -Depth 20 | Set-Content $recordPath -Encoding utf8
    @{state='Active';processFile=$recordPath;caseId=$Case} | ConvertTo-Json | Set-Content $marker -Encoding utf8
    $load=& (Join-Path $root 'scripts/simulate-station01-load.ps1') -PrepareOnly -FixtureManifest $prepared -OutputDirectory $run
    $env:GAODE_TEST_PREPARED_LOAD_PATH=[IO.Path]::GetFullPath($load)
    $env:GAODE_FRONTEND_DIST=Join-Path (Split-Path $desktopExe) 'frontend/dist'
    $env:WEBVIEW2_USER_DATA_FOLDER=Join-Path $run 'webview2-profile'
    $record.preparedLoad=$load
    if (-not $Headless) {
        $desktop=Start-Process $desktopExe -WorkingDirectory (Split-Path $desktopExe) -PassThru
        $record.desktopPid=$desktop.Id
        $record.ownedProcessStartUtc.desktopPid=(Get-CimInstance Win32_Process -Filter "ProcessId = $($desktop.Id)").CreationDate.ToUniversalTime().ToString('o')
        $record | ConvertTo-Json -Depth 20 | Set-Content $recordPath -Encoding utf8
        try { Start-Process ($plcApi + '/?monitor=unlock-history-r9') | Out-Null } catch { Write-Warning "请手动打开 $plcApi" }
    }
    $record | ConvertTo-Json -Depth 20 | Set-Content $recordPath -Encoding utf8
    Write-Host "已就绪：$Case；配方 $($fixture.recipeRef.recipeId) / $($fixture.recipeRef.version)"
    Write-Host '页面点击进入系统，选择配方并启动；允许取盘后点击取盘确认。'
    Write-Host "PLC监控：$plcApi；数据与日志：$run"
    Write-Host '测试结束请双击 Stop.cmd。'
} catch {
    if ((Test-Path $marker) -and (Get-Content $marker -Raw | ConvertFrom-Json).state -eq 'Active') {
        & (Join-Path $root 'Stop.ps1')
    }
    throw
}

