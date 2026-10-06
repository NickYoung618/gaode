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
# Hold verified process handles before ending Host, which owns the Python child.
$owned=@{}; $trace=@(); $completed=$false
try {
    foreach ($key in @('desktopPid','hostPid','workerPid','plcPid')) {
        if(-not $record[$key]){continue}
        $held=Get-Process -Id ([int]$record[$key]) -ErrorAction SilentlyContinue
        if(-not $held){$trace+=@{role=$key;pid=$record[$key];state='AlreadyExited'};continue}
        $owned[$key]=$held
        $null=$held.Handle
        if($held.HasExited){$trace+=@{role=$key;pid=$record[$key];state='AlreadyExited'};continue}
        $actual=Get-CimInstance Win32_Process -Filter "ProcessId=$([int]$record[$key])"
        $expectedStart=[datetimeoffset]$record.ownedProcessStartUtc[$key]
        # WMI's creation timestamp has microsecond precision; compare the retained handle at the same precision.
        $handleMatches=$held.StartTime.ToUniversalTime().ToString('yyyyMMddHHmmssffffff') -eq $expectedStart.UtcDateTime.ToString('yyyyMMddHHmmssffffff')
        $creationMatches=$actual -and ([datetimeoffset]$actual.CreationDate.ToUniversalTime()) -eq $expectedStart
        $pathMatches=$actual -and ([string]$actual.CommandLine).Contains($expected[$key],[StringComparison]::OrdinalIgnoreCase)
        $trace+=@{role=$key;pid=$record[$key];expectedStart=$expectedStart;handleStartUtc=$held.StartTime.ToUniversalTime();creationMatches=[bool]$creationMatches;pathMatches=[bool]$pathMatches;handleMatches=$handleMatches;state='Prechecked'}
        if(-not ($handleMatches -and $creationMatches -and $pathMatches)){throw "跳过 PID $($record[$key])：进程身份与本次记录不符，未开始停止。"}
    }
    foreach($key in @('desktopPid','hostPid','workerPid','plcPid')) {
        $held=$owned[$key]
        if(-not $held){continue}
        if(-not $held.HasExited) {
            try {$held.Kill()} catch {if(-not $held.HasExited){throw}}
            if(-not $held.WaitForExit(5000)){throw "本次 $key 尚未退出。"}
        }
        $trace+=@{role=$key;pid=$record[$key];state='Exited';utc=[datetime]::UtcNow.ToString('o')}
        Write-Host "已退出 $key。"
    }
    $session.state='Stopped'
    $session | ConvertTo-Json | Set-Content -LiteralPath $marker -Encoding utf8
    $completed=$true
    Write-Host '本次平台已停止。PLC监控浏览器窗口请手动关闭；运行数据留在 artifacts/recipe-execution-008。'
} finally {
    @{utc=[datetime]::UtcNow.ToString('o');completed=$completed;processes=$trace;method='Verify all identities before stop; terminate retained process handles only'} | ConvertTo-Json -Depth 7 | Set-Content (Join-Path (Split-Path $processPath) 'stop-diagnostic.json') -Encoding utf8
    foreach($held in $owned.Values){$held.Dispose()}
}
