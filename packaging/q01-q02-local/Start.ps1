param([ValidateSet('Q01', 'Q02')][string]$Case = 'Q01')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot)
$runBase = Join-Path $root 'artifacts/recipe-execution-008'
$marker = Join-Path $runBase 'local-active.json'
if ([Diagnostics.Process]::GetCurrentProcess().SessionId -eq 0) {
    throw '请在已登录的 Windows 桌面会话中运行，WPF 页面无法在 Session 0 显示。'
}
if (Test-Path -LiteralPath $marker) {
    $previous = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ($previous.state -eq 'Active') { throw '已有本压缩包启动的会话。请先运行 Stop.ps1。' }
}
foreach ($name in @('dotnet', 'python', 'pwsh')) { Get-Command $name -ErrorAction Stop | Out-Null }
foreach ($port in @(5001, 5080, 1502)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
        throw "本机端口 $port 已被使用。请先结束原测试会话。"
    }
}
$suffix = if ($Case -eq 'Q01') { '' } else { '-q02' }
$template = Join-Path $root "specs/008-recipe-driven-inspection/fixtures/fixture$suffix.json"
$fixture = Get-Content -LiteralPath $template -Raw | ConvertFrom-Json -AsHashtable
$fixtureDir = Join-Path $root 'specs/008-recipe-driven-inspection/fixtures'
$fixture.configRoot = Join-Path $root 'specs/007-station01-integrated-loop/examples'
$fixture.recipeCatalogPath = Join-Path $fixtureDir "recipes$suffix.json"
$fixture.imageManifestPath = Join-Path $fixtureDir "media-manifest$suffix.json"
$fixture.workerManifestPath = Join-Path $fixtureDir "worker-manifest$suffix.json"
$fixture.workerScriptPath = Join-Path $root 'scripts/virtual-station01-algorithm.py'
$actualDigest = (Get-FileHash -LiteralPath $fixture.recipeCatalogPath -Algorithm SHA256).Hash
if ($actualDigest -ne $fixture.recipeRef.catalogDigest) { throw '配方目录与已验证版本摘要不一致。' }
foreach ($image in (Get-Content -LiteralPath $fixture.imageManifestPath -Raw | ConvertFrom-Json).images) {
    $path = [IO.Path]::GetFullPath((Join-Path $fixtureDir $image.relativePath))
    if (-not (Test-Path -LiteralPath $path) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $image.sha256) {
        throw "采集图片缺失或摘要不符：$($image.role)"
    }
}
$run = Join-Path $runBase ("local-$Case-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$fixturePath = Join-Path $runBase ("prepared-$Case-" + [guid]::NewGuid().ToString('N') + '.json')
New-Item -ItemType Directory -Path $runBase -Force | Out-Null
$fixture | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $fixturePath -Encoding utf8
$oldToken = $env:GAODE_TEST_OPERATOR_TOKEN
$oldPrepared = $env:GAODE_TEST_PREPARED_LOAD_PATH
$oldApi = $env:GAODE_API_BASE_URL
$oldHub = $env:GAODE_SIGNALR_URL
$oldMode = $env:GAODE_MODE
$oldFrontend = $env:GAODE_FRONTEND_DIST
$oldScan = $env:Simulation__ScanPeriodMs
try {
    $token = [guid]::NewGuid().ToString('N')
    $env:GAODE_TEST_OPERATOR_TOKEN = $token
    $env:Simulation__ScanPeriodMs = '20'
    $hostDll = Join-Path $root 'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll'
    $plcDll = Join-Path $root 'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll'
    $desktopExe = Join-Path $root 'desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe'
    foreach ($file in @($hostDll, $plcDll, $desktopExe)) {
        if (-not (Test-Path -LiteralPath $file)) { throw "运行文件缺失：$file" }
    }
    & (Join-Path $root 'scripts/start-station01-virtual-loop.ps1') -OperatorToken $token `
        -TestRoot $run -FixtureManifest $fixturePath -HostDll $hostDll -PlcDll $plcDll -SkipDesktop | Out-Null
    $recordPath = Join-Path $run 'process.json'
    $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json -AsHashtable
    if (-not $record.hostPid -or -not $record.plcPid -or -not $record.workerPid) {
        throw "平台没有完整就绪。查看 $recordPath 和 logs。"
    }
    $loadPath = & (Join-Path $root 'scripts/simulate-station01-load.ps1') `
        -PrepareOnly -FixtureManifest $fixturePath -OutputDirectory $run
    if (-not (Test-Path -LiteralPath $loadPath)) { throw '上料准备未生成。' }
    $env:GAODE_TEST_PREPARED_LOAD_PATH = [IO.Path]::GetFullPath($loadPath)
    $env:GAODE_API_BASE_URL = 'http://127.0.0.1:5001'
    $env:GAODE_SIGNALR_URL = 'http://127.0.0.1:5001/hubs/station01'
    $env:GAODE_MODE = 'Test'
    $env:GAODE_FRONTEND_DIST = Join-Path (Split-Path $desktopExe) 'frontend/dist'
    $env:WEBVIEW2_USER_DATA_FOLDER = Join-Path $run 'webview2-profile'
    $desktop = Start-Process -FilePath $desktopExe -WorkingDirectory (Split-Path $desktopExe) -PassThru
    $record.desktopPid = $desktop.Id
    $record.preparedLoad = $loadPath
    $record.caseId = $Case
    $record.fixtureManifest = $fixturePath
    $record.ownedProcessStartUtc = @{}
    foreach ($key in @('hostPid', 'plcPid', 'workerPid', 'desktopPid')) {
        $process = Get-CimInstance Win32_Process -Filter "ProcessId = $([int]$record[$key])"
        if (-not $process) { throw "进程 $key 已退出；查看 $recordPath。" }
        $record.ownedProcessStartUtc[$key] = $process.CreationDate.ToUniversalTime().ToString('o')
    }
    $record | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $recordPath -Encoding utf8
    @{ state = 'Active'; processFile = $recordPath; caseId = $Case } |
        ConvertTo-Json | Set-Content -LiteralPath $marker -Encoding utf8
    try {
        $edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
                  "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") |
            Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if ($edge) { Start-Process -FilePath $edge -ArgumentList @('--new-window', 'http://127.0.0.1:5080/') | Out-Null }
        else { Start-Process 'http://127.0.0.1:5080/' | Out-Null }
    } catch { Write-Warning 'PLC监控页未自动打开，请访问 http://127.0.0.1:5080/' }
    Write-Host "已启动 $Case：前端窗口、后端、虚拟PLC、算法进程。"
    Write-Host '在前端点击“进入系统”，选择本次配方，点击“启动”，解锁后点击“取盘确认”。'
    Write-Host 'PLC监控：http://127.0.0.1:5080/'
    Write-Host "本次记录：$recordPath"
    Write-Host '完成后运行：pwsh -NoProfile -File .\Stop.ps1'
} finally {
    $env:GAODE_TEST_OPERATOR_TOKEN = $oldToken
    $env:GAODE_TEST_PREPARED_LOAD_PATH = $oldPrepared
    $env:GAODE_API_BASE_URL = $oldApi
    $env:GAODE_SIGNALR_URL = $oldHub
    $env:GAODE_MODE = $oldMode
    $env:GAODE_FRONTEND_DIST = $oldFrontend
    $env:Simulation__ScanPeriodMs = $oldScan
}
