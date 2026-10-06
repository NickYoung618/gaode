<#
.SYNOPSIS
Stop only the processes recorded by the first-station manual Test launcher.
#>
[CmdletBinding()]
param(
    [string]$ProcessFile = ''
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/station01-007'))
$markerPath = Join-Path $allowedRoot 'manual-active.json'
if (-not $ProcessFile) {
    if (-not (Test-Path -LiteralPath $markerPath)) {
        throw '没有本次一键启动记录。可用 -ProcessFile 指定对应 TestRoot 下的 process.json。'
    }
    $marker = Get-Content -Raw -LiteralPath $markerPath | ConvertFrom-Json
    $ProcessFile = $marker.processFile
}
if (-not $ProcessFile) { throw '进程记录路径为空。' }
$ProcessFile = [IO.Path]::GetFullPath($ProcessFile)
if (-not $ProcessFile.StartsWith($allowedRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($ProcessFile) -ne 'process.json' -or
    -not (Test-Path -LiteralPath $ProcessFile)) {
    throw '进程记录必须是本项目 artifacts/station01-007 下现存的 process.json。'
}
$testRoot = [IO.Path]::GetDirectoryName($ProcessFile)
$record = Get-Content -Raw -LiteralPath $ProcessFile | ConvertFrom-Json -AsHashtable
if ([IO.Path]::GetFullPath([string]$record['testRoot']) -ne $testRoot) {
    throw '进程记录中的 TestRoot 与文件位置不一致。'
}

$specs = @(
    @{ key = 'desktopPid'; name = 'Gaode.Station01.Desktop.exe';
        fragment = (Join-Path $repo 'desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe') },
    @{ key = 'watchPid'; name = 'pwsh.exe';
        fragment = (Join-Path $repo 'scripts/watch-station01-auto-removal.ps1'); second = $testRoot },
    @{ key = 'hostPid'; name = 'dotnet.exe';
        fragment = (Join-Path $repo 'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll') },
    @{ key = 'workerPid'; name = 'python.exe';
        fragment = (Join-Path $repo 'scripts/virtual-station01-algorithm.py') },
    @{ key = 'plcPid'; name = 'dotnet.exe';
        fragment = (Join-Path $repo 'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll') }
)
$problems = @()
foreach ($spec in $specs) {
    $ownedPid = $record[$spec.key]
    if (-not $ownedPid) { continue }
    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $([int]$ownedPid)" -ErrorAction SilentlyContinue
    if (-not $process) {
        Write-Host "$($spec.key) PID $ownedPid 已退出。"
        continue
    }
    $command = [string]$process.CommandLine
    $recordedStart = if ($record['ownedProcessStartUtc']) {
        ([datetime]$record['ownedProcessStartUtc'][$spec.key]).ToUniversalTime().ToString('o')
    } else { '' }
    $actualStart = $process.CreationDate.ToUniversalTime().ToString('o')
    $matches = $process.Name -ieq $spec.name -and
        $recordedStart -eq $actualStart -and
        $command.Contains($spec.fragment, [StringComparison]::OrdinalIgnoreCase) -and
        (-not $spec.second -or $command.Contains($spec.second, [StringComparison]::OrdinalIgnoreCase))
    if (-not $matches) {
        $problems += "$($spec.key) PID $ownedPid 的进程身份与本次记录不符，已跳过"
        continue
    }
    try {
        Stop-Process -Id ([int]$ownedPid) -ErrorAction Stop
        Write-Host "已停止 $($spec.key) PID $ownedPid。"
    }
    catch {
        if (Get-Process -Id ([int]$ownedPid) -ErrorAction SilentlyContinue) {
            $problems += "$($spec.key) PID $ownedPid 停止失败：$($_.Exception.Message)"
        }
    }
}

$remaining = @()
for ($attempt = 0; $attempt -lt 10; $attempt++) {
    $remaining = @(Get-NetTCPConnection -State Listen -LocalPort 5001, 5080, 1502 -ErrorAction SilentlyContinue)
    if ($remaining.Count -eq 0) { break }
    Start-Sleep -Milliseconds 500
}
if ($remaining.Count -gt 0) {
    $ports = ($remaining | ForEach-Object { "$($_.LocalPort)(PID $($_.OwningProcess))" } | Sort-Object -Unique) -join ', '
    $problems += "端口仍被占用：$ports；未按端口号结束其他进程"
}
$record['stopAttemptAtUtc'] = [datetime]::UtcNow.ToString('o')
$record['stopStatus'] = if ($problems.Count -eq 0) { 'Stopped' } else { 'NeedsReview' }
$record | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $ProcessFile -Encoding utf8
if ((Test-Path -LiteralPath $markerPath)) {
    $marker = Get-Content -Raw -LiteralPath $markerPath | ConvertFrom-Json -AsHashtable
    if ([string]$marker['processFile'] -eq $ProcessFile) {
        $marker['state'] = $record['stopStatus']
        $marker['stopAttemptAtUtc'] = $record['stopAttemptAtUtc']
        $marker | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $markerPath -Encoding utf8
    }
}
if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Warning $_ }
    throw "部分进程或端口需要人工核对。记录：$ProcessFile"
}
Write-Host "本次启动的进程已停止，5001/5080/1502 无监听。记录：$ProcessFile"
