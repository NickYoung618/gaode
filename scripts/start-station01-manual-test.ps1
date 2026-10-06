<#
.SYNOPSIS
Build and start the first-station virtual platform, PLC monitor and WPF page for a manual Test run.

.DESCRIPTION
The script prepares a new CAP/P01 load request but does not submit it. Click the
existing Start button in the WebView2 page to start the actual run.
The read-only PLC monitor opens in Edge when installed, otherwise in the default browser.
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot = Join-Path $repo ('artifacts/station01-007/manual-' +
    (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$desktopExe = Join-Path $repo 'desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe'
$desktopRuntime = Join-Path $repo 'desktop/bin/Release/net10.0-windows10.0.17763.0/frontend/dist/runtime.js'
$frontendRuntime = Join-Path $repo 'frontend/dist/runtime.js'
$activeMarker = Join-Path $repo 'artifacts/station01-007/manual-active.json'
$oldOperatorToken = [Environment]::GetEnvironmentVariable('GAODE_TEST_OPERATOR_TOKEN', 'Process')
$oldPreparedPath = [Environment]::GetEnvironmentVariable('GAODE_TEST_PREPARED_LOAD_PATH', 'Process')
$oldApiBase = [Environment]::GetEnvironmentVariable('GAODE_API_BASE_URL', 'Process')
$oldSignalR = [Environment]::GetEnvironmentVariable('GAODE_SIGNALR_URL', 'Process')
$oldMode = [Environment]::GetEnvironmentVariable('GAODE_MODE', 'Process')
$launchPhases = [Collections.Generic.List[object]]::new()
$launchPhase = 'Prerequisites'
function Add-LaunchPhase([string]$Name) {
    $script:launchPhase = $Name
    $launchPhases.Add([ordered]@{ utc = [datetime]::UtcNow.ToString('o'); phase = $Name })
}

Push-Location $repo
try {
    Add-LaunchPhase 'Prerequisites'
    if ([Diagnostics.Process]::GetCurrentProcess().SessionId -eq 0) {
        throw '请在本地 Windows 交互桌面的 PowerShell 中运行，Session 0 无法操作 WPF 页面。'
    }
    $occupied = @(Get-NetTCPConnection -State Listen -LocalPort 5001, 5080, 1502 -ErrorAction SilentlyContinue)
    if ($occupied.Count -gt 0) {
        $details = ($occupied | ForEach-Object { "$($_.LocalPort)(PID $($_.OwningProcess))" } | Sort-Object -Unique) -join ', '
        throw "测试端口已被占用：$details。先确认原会话已结束；本脚本不会停止已有进程。"
    }
    Get-Command dotnet, npm, node, python, pwsh -ErrorAction Stop | Out-Null
    Write-Host '检查 PLC 监控页面脚本（不启动设备或提交检测请求）...'
    & node --test (Join-Path $PSScriptRoot 'tests/virtual-plc-monitor.test.cjs')
    if ($LASTEXITCODE -ne 0) { throw 'PLC 监控页面脚本检查失败；尚未启动本次 Test 平台。' }
    if (-not $SkipBuild) {
        Add-LaunchPhase 'BuildFrontend'
        if (-not (Test-Path -LiteralPath 'frontend/node_modules/.package-lock.json')) {
            & npm --prefix frontend ci
            if ($LASTEXITCODE -ne 0) { throw '前端依赖安装失败。' }
        }
        & npm --prefix frontend run build
        if ($LASTEXITCODE -ne 0) { throw '前端构建失败。' }
        Add-LaunchPhase 'BuildHost'
        & dotnet build backend/src/Gaode.Host/Gaode.Host.csproj -c Debug
        if ($LASTEXITCODE -ne 0) { throw '后端构建失败。' }
        Add-LaunchPhase 'BuildVirtualPlc'
        & dotnet build VirtualPlc/VirtualPlc.csproj -c Debug
        if ($LASTEXITCODE -ne 0) { throw 'VirtualPlc 构建失败。' }
        Add-LaunchPhase 'BuildDesktop'
        & dotnet build desktop/Gaode.Station01.Desktop.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw '桌面程序构建失败。' }
    }
    if (-not (Test-Path -LiteralPath $desktopExe) -or
        -not (Test-Path -LiteralPath $frontendRuntime) -or
        -not (Test-Path -LiteralPath $desktopRuntime)) {
        throw '桌面程序或前端资源缺失；请不带 -SkipBuild 重新运行。'
    }
    $frontendHash = (Get-FileHash -LiteralPath $frontendRuntime -Algorithm SHA256).Hash
    $desktopRuntimeHash = (Get-FileHash -LiteralPath $desktopRuntime -Algorithm SHA256).Hash
    if ($frontendHash -ne $desktopRuntimeHash) {
        throw '桌面程序中的前端资源与当前构建不一致；请不带 -SkipBuild 重新运行。'
    }

    $operatorToken = $env:GAODE_TEST_OPERATOR_TOKEN
    if ([string]::IsNullOrWhiteSpace($operatorToken)) {
        $secret = Read-Host '输入受控本地 Test Operator Token' -AsSecureString
        $operatorToken = [Net.NetworkCredential]::new('', $secret).Password
    }
    if ([string]::IsNullOrWhiteSpace($operatorToken)) { throw 'Test Operator Token 不能为空。' }
    $env:GAODE_TEST_OPERATOR_TOKEN = $operatorToken

    Write-Host "启动独立 Test 平台：$testRoot"
    Add-LaunchPhase 'StartPlatformAndWaitReady'
    # Invoke the existing script directly. Piping a child pwsh through Select-Object
    # can keep waiting on handles inherited by its long-lived Host/PLC children.
    & (Join-Path $PSScriptRoot 'start-station01-virtual-loop.ps1') -SkipDesktop -TestRoot $testRoot
    $processPath = Join-Path $testRoot 'process.json'
    if (-not (Test-Path -LiteralPath $processPath)) {
        throw "平台启动失败。查看 $testRoot\process.json 和 logs；本脚本不会停止已启动的进程。"
    }
    $record = Get-Content -Raw -LiteralPath $processPath | ConvertFrom-Json -AsHashtable
    $processStarts = @{}
    foreach ($key in @('plcPid', 'hostPid', 'workerPid', 'watchPid')) {
        if (-not $record[$key]) { throw "平台未记录 $key；请检查 $processPath。" }
        $startedProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $([int]$record[$key])"
        if (-not $startedProcess) { throw "平台进程 $key 已退出；请检查 $processPath。" }
        $processStarts[$key] = $startedProcess.CreationDate.ToUniversalTime().ToString('o')
    }
    $record['ownedProcessStartUtc'] = $processStarts
    $record | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $processPath -Encoding utf8
    [ordered]@{ processFile = [IO.Path]::GetFullPath($processPath); state = 'Active';
        startedAtUtc = [datetime]::UtcNow.ToString('o') } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $activeMarker -Encoding utf8
    $expectedPorts = @{ 5001 = [int]$record['hostPid']; 5080 = [int]$record['plcPid'];
        1502 = [int]$record['plcPid'] }
    foreach ($port in $expectedPorts.Keys) {
        $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)
        if (-not ($listeners | Where-Object { $_.OwningProcess -eq $expectedPorts[$port] })) {
            throw "端口 $port 未由本次记录的进程 PID $($expectedPorts[$port]) 监听；请检查 $processPath。"
        }
    }

    Add-LaunchPhase 'PrepareLoadRequest'
    $loadPath = & (Join-Path $PSScriptRoot 'simulate-station01-load.ps1') `
        -PrepareOnly -Scenario S1 -OccupiedSlots P01 -OutputDirectory $testRoot
    if (-not $loadPath -or -not (Test-Path -LiteralPath $loadPath)) {
        throw "CAP/P01 上料请求准备失败。查看 $testRoot；本脚本不会停止已启动的进程。"
    }

    $env:GAODE_TEST_PREPARED_LOAD_PATH = [IO.Path]::GetFullPath($loadPath)
    $env:GAODE_API_BASE_URL = 'http://127.0.0.1:5001'
    $env:GAODE_SIGNALR_URL = 'http://127.0.0.1:5001/hubs/station01'
    $env:GAODE_MODE = 'Test'
    Add-LaunchPhase 'LaunchDesktop'
    $desktop = Start-Process -FilePath $desktopExe -WorkingDirectory (Split-Path $desktopExe -Parent) -PassThru
    $desktopStarted = Get-CimInstance Win32_Process -Filter "ProcessId = $($desktop.Id)"
    if (-not $desktopStarted) { throw '桌面程序启动后立即退出；平台进程可通过关闭脚本清理。' }

    $record['desktopPid'] = $desktop.Id
    $record['ownedProcessStartUtc']['desktopPid'] = $desktopStarted.CreationDate.ToUniversalTime().ToString('o')
    $record['desktop'] = 'Started for manual WebView2 page test; Start button not clicked by script'
    $record['preparedLoad'] = [IO.Path]::GetFullPath($loadPath)
    $record['frontendRuntimeSha256'] = $frontendHash
    $record['desktopExeSha256'] = (Get-FileHash -LiteralPath $desktopExe -Algorithm SHA256).Hash
    $record['desktopRuntimeLog'] = Join-Path $testRoot 'logs/desktop-runtime.jsonl'

    # Open from the interactive launcher, not from the background PLC process.
    # The browser can reuse a user's existing process; the stop script must not own it.
    $plcMonitorUrl = ([string]$record['plcApiBase']).TrimEnd('/') + '/'
    $record['plcMonitor'] = [ordered]@{ url = $plcMonitorUrl; state = 'NotRequested' }
    try {
        $edgePath = @(
            "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
            "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
            "$env:LOCALAPPDATA\Microsoft\Edge\Application\msedge.exe"
        ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if ($edgePath) {
            Start-Process -FilePath $edgePath -ArgumentList @('--new-window', $plcMonitorUrl) -ErrorAction Stop
        } else {
            Start-Process -FilePath $plcMonitorUrl -ErrorAction Stop
        }
        $record['plcMonitor']['state'] = 'OpenRequested'
        Write-Host "已请求打开 PLC 实时监控：$plcMonitorUrl（请确认浏览器页面已显示）"
    }
    catch {
        $record['plcMonitor']['state'] = 'OpenFailed'
        $record['plcMonitor']['error'] = $_.Exception.Message
        Write-Warning "PLC 监控页面自动打开失败：$($_.Exception.Message)。请手动打开 $plcMonitorUrl；此结果不代表 PLC 服务已停止。"
    }
    $record | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $processPath -Encoding utf8

    Add-LaunchPhase 'LaunchCompleted_StartNotSubmitted'
    Write-Host ''
    Write-Host '平台和桌面程序已启动。请在页面点“进入系统”，再点原有“启动”按钮一次。'
    Write-Host '算法 worker 由 Host 管理；虚拟相机无需单独启动。'
    Write-Host "VirtualPlc PID：$($record['plcPid'])（端口 5080、1502）"
    Write-Host "Host PID：$($record['hostPid'])（端口 5001）；worker PID：$($record['workerPid'])"
    Write-Host "本次进程记录：$processPath"
    Write-Host "准备的上料请求：$loadPath"
    Write-Host "桌面 PID：$($desktop.Id)"
    Write-Host '测试完成后双击 scripts\stop-station01-manual-test.cmd，只关闭本次记录的进程。'
    Write-Host 'PLC 监控浏览器窗口请手动关闭；关闭脚本不会结束你的其他浏览器窗口。'
}
catch {
    $safeError = $_.Exception.ToString()
    if ($operatorToken) { $safeError = $safeError.Replace($operatorToken, '[REDACTED]') }
    $launchPhases.Add([ordered]@{ utc = [datetime]::UtcNow.ToString('o'); phase = $launchPhase;
        outcome = 'Failed'; exception = $safeError; disposition = 'ExistingProcessesUntouched_InspectProcessRecord' })
    throw
}
finally {
    # Do not populate TestRoot before StorePrep's empty-root check.
    try {
        $launchLogRoot = Join-Path $testRoot 'logs'
        New-Item -ItemType Directory -Path $launchLogRoot -Force | Out-Null
        $launchPhases | ForEach-Object { $_ | ConvertTo-Json -Depth 4 -Compress } |
            Set-Content -LiteralPath (Join-Path $launchLogRoot 'launcher.jsonl') -Encoding utf8
    } catch { Write-Warning '启动诊断记录保存失败，请保留终端错误信息。' }
    Pop-Location
    [Environment]::SetEnvironmentVariable('GAODE_TEST_OPERATOR_TOKEN', $oldOperatorToken, 'Process')
    [Environment]::SetEnvironmentVariable('GAODE_TEST_PREPARED_LOAD_PATH', $oldPreparedPath, 'Process')
    [Environment]::SetEnvironmentVariable('GAODE_API_BASE_URL', $oldApiBase, 'Process')
    [Environment]::SetEnvironmentVariable('GAODE_SIGNALR_URL', $oldSignalR, 'Process')
    [Environment]::SetEnvironmentVariable('GAODE_MODE', $oldMode, 'Process')
}
