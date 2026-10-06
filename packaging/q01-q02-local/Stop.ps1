$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot)
$base = Join-Path $root 'artifacts/recipe-execution-008'
$marker = Join-Path $base 'local-active.json'
if (-not (Test-Path -LiteralPath $marker)) { throw '没有本压缩包的启动记录。' }
$session = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
if ($session.state -ne 'Active') { throw '没有活动的本压缩包会话。' }
$processPath = [IO.Path]::GetFullPath($session.processFile)
if (-not $processPath.StartsWith($base + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($processPath) -ne 'process.json') {
    throw '进程记录不属于本压缩包。'
}
$record = Get-Content -LiteralPath $processPath -Raw | ConvertFrom-Json -AsHashtable
$expected = @{
    desktopPid = Join-Path $root 'desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe'
    hostPid = Join-Path $root 'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll'
    workerPid = Join-Path $root 'scripts/virtual-station01-algorithm.py'
    plcPid = Join-Path $root 'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll'
}
foreach ($key in @('desktopPid', 'hostPid', 'workerPid', 'plcPid')) {
    if (-not $record[$key]) { continue }
    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $([int]$record[$key])"
    if (-not $process) { continue }
    $start = $process.CreationDate.ToUniversalTime().ToString('o')
    if ($start -ne [string]$record.ownedProcessStartUtc[$key] -or
        -not ([string]$process.CommandLine).Contains($expected[$key], [StringComparison]::OrdinalIgnoreCase)) {
        throw "跳过 PID $($record[$key])：进程身份与本次记录不符。"
    }
    Stop-Process -Id ([int]$record[$key]) -ErrorAction Stop
    Write-Host "已停止 $key。"
}
$session.state = 'Stopped'
$session | ConvertTo-Json | Set-Content -LiteralPath $marker -Encoding utf8
Write-Host '本次平台已停止。PLC监控浏览器窗口请手动关闭；运行数据留在 artifacts/recipe-execution-008。'
