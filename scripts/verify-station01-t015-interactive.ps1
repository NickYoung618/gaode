param([Parameter(Mandatory)][string]$EvidenceRoot,
      [int]$ApiPort = 5321, [int]$PlcApiPort = 5401, [int]$ModbusPort = 1621,
      [int]$NormalDebugPort = 9241, [switch]$AuthOnly, [switch]$Only403)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/station01-007'))
$root = [IO.Path]::GetFullPath($EvidenceRoot)
if (-not $root.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence root is outside isolated 007 artifacts.' }
if ((Get-Process -Id $PID).SessionId -eq 0) { throw 'Actual WPF verification requires an interactive session.' }
if ((Test-Path $root) -and @(Get-ChildItem -LiteralPath $root -Force).Count -gt 0) { throw 'Evidence root must be new and empty.' }
foreach ($port in @($ApiPort,$PlcApiPort,$ModbusPort,$NormalDebugPort,($NormalDebugPort+1),($NormalDebugPort+2))) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { throw "Isolated port already in use: $port" }
}
$hostDll = Join-Path $repo 'artifacts/station01-007/t015-current-binaries-20260924/host/Gaode.Host.dll'
$plcDll = Join-Path $repo 'artifacts/station01-007/t015-current-binaries-20260924/plc/VirtualPlc.dll'
$desktop = Join-Path $repo 'artifacts/station01-007/t015-current-desktop-build-20260924/publish/Gaode.Station01.Desktop.exe'
$desktopDll = Join-Path $repo 'artifacts/station01-007/t015-current-desktop-build-20260924/publish/Gaode.Station01.Desktop.dll'
$dist = Join-Path $repo 'artifacts/station01-007/t015-current-desktop-build-20260924/publish/frontend/dist'
foreach ($file in @($hostDll,$plcDll,$desktop,$desktopDll,(Join-Path $dist 'runtime.js'))) { if (-not (Test-Path $file -PathType Leaf)) { throw "Missing current binary: $file" } }
$api = "http://127.0.0.1:$ApiPort"; $plcApi = "http://127.0.0.1:$PlcApiPort"
$operatorToken = [guid]::NewGuid().ToString('N'); $engineerToken = [guid]::NewGuid().ToString('N')
$env:Gaode__Tokens__EquipmentEngineer = $engineerToken
$owned = [System.Collections.Generic.List[object]]::new()
$phase = 'preflight'; $exitCode = 1
function Save-Json([string]$name, $value) { $value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $root $name) -Encoding utf8 }
function Start-Desktop([string]$kind, [string]$token, [int]$port, [string]$prepared) {
    $env:GAODE_MODE = 'Test'; $env:GAODE_API_BASE_URL = $api; $env:GAODE_SIGNALR_URL = "$api/hubs/station01"
    $env:GAODE_TEST_OPERATOR_TOKEN = $token; $env:GAODE_TEST_PREPARED_LOAD_PATH = $prepared
    $env:GAODE_FRONTEND_DIST = $dist; $env:WEBVIEW2_USER_DATA_FOLDER = Join-Path $root "profile-$kind"
    $env:GAODE_TEST_WEBVIEW2_DEBUG_PORT = [string]$port
    $process = Start-Process -FilePath $desktop -WorkingDirectory (Split-Path $desktop) -WindowStyle Normal -PassThru
    $owned.Add([pscustomobject]@{ kind="desktop-$kind"; pid=$process.Id; expected=$desktop })
    for ($i=0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 300
        try { $null = Invoke-RestMethod "http://127.0.0.1:$port/json" -TimeoutSec 2; break } catch { }
    }
    $actual = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
    Save-Json "$kind-interactive-desktop.json" ([ordered]@{ atUtc=[datetime]::UtcNow.ToString('o'); processId=$process.Id; sessionId=$actual.SessionId;
        mainWindowHandle=[string]$actual.MainWindowHandle; executable=$desktop; exeSha256=(Get-FileHash $desktop -Algorithm SHA256).Hash;
        desktopDll=$desktopDll; desktopDllSha256=(Get-FileHash $desktopDll -Algorithm SHA256).Hash;
        frontendRuntime=Join-Path $dist 'runtime.js'; frontendRuntimeSha256=(Get-FileHash (Join-Path $dist 'runtime.js') -Algorithm SHA256).Hash;
        preparedLoadPath=$prepared; preparedLoadSha256=(Get-FileHash $prepared -Algorithm SHA256).Hash; apiBase=$api; debugPort=$port;
        source='Test/actual-WPF-WebView2' })
    return $process
}
function Stop-Owned([int]$id,[string]$expected) {
    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $id" -ErrorAction SilentlyContinue
    if (-not $process) { return }
    $same = [string]::Equals([IO.Path]::GetFullPath([string]$process.ExecutablePath),[IO.Path]::GetFullPath($expected),[StringComparison]::OrdinalIgnoreCase)
    if (-not $same) { throw "PID $id identity mismatch; will not stop it" }
    Stop-Process -Id $id -ErrorAction Stop
}
try {
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $phase = 'platform'
    $null = & (Join-Path $PSScriptRoot 'start-station01-virtual-loop.ps1') -OperatorToken $operatorToken -TestRoot $root -ApiBase $api -PlcApiBase $plcApi -PlcPort $ModbusPort -HostDll $hostDll -PlcDll $plcDll -SkipDesktop
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Platform start failed' }
    $platform = Get-Content -LiteralPath (Join-Path $root 'process.json') -Raw | ConvertFrom-Json
    $owned.Add([pscustomobject]@{kind='host';pid=[int]$platform.hostPid;expected=(Get-Command dotnet).Source})
    $owned.Add([pscustomobject]@{kind='plc';pid=[int]$platform.plcPid;expected=(Get-Command dotnet).Source})
    if ($platform.watchPid) { $owned.Add([pscustomobject]@{kind='watch';pid=[int]$platform.watchPid;expected=(Get-Command pwsh).Source}) }
    $phase = 'prepare'
    $prepared = [string](& (Join-Path $PSScriptRoot 'simulate-station01-load.ps1') -PrepareOnly -OutputDirectory $root -ApiBase $api)
    if (-not (Test-Path $prepared -PathType Leaf)) { throw 'PrepareOnly did not produce load request' }
    $prepared = (Resolve-Path $prepared).Path
    if (-not $AuthOnly) {
        $phase = 'normal-page'
        $normal = Start-Desktop 'normal' $operatorToken $NormalDebugPort $prepared
        & node (Join-Path $PSScriptRoot 'capture-station01-webview2-normal.cjs') $NormalDebugPort $root Normal 1> (Join-Path $root 'normal-capture.out.log') 2> (Join-Path $root 'normal-capture.err.log')
        $normalExit = $LASTEXITCODE
        if ($normalExit -ne 0) { throw "Actual normal page capture failed: $normalExit" }
        $page = Get-Content -LiteralPath (Join-Path $root 'normal-webview2-page-evidence.json') -Raw | ConvertFrom-Json
        Copy-Item -LiteralPath (Join-Path $root 'normal-webview2-page-evidence.json') -Destination (Join-Path $root 'webview2-page-evidence.json') -ErrorAction Stop
        Copy-Item -LiteralPath (Join-Path $root 'normal-interactive-desktop.json') -Destination (Join-Path $root 'interactive-desktop.json') -ErrorAction Stop
        if ($page.receipt.status -eq 202 -and $page.receipt.body.runId) {
            $phase = 'normal-facts'
            $null = & (Join-Path $PSScriptRoot 'collect-station01-page-facts.ps1') -EvidenceRoot $root -OperatorToken $operatorToken -ApiBase $api -PlcApiBase $plcApi
        }
        Stop-Owned $normal.Id $desktop
        if ($page.outcome -ne 'FinalPageDisplayed') { throw "Normal WPF run not Final: $($page.outcome); no automatic retry" }
    }
    $phase = 'authorization-pages'
    $authCases = if ($Only403) { @(@{kind='Auth403';token=$engineerToken;port=$NormalDebugPort+2}) }
        else { @(@{kind='Auth401';token='';port=$NormalDebugPort+1},@{kind='Auth403';token=$engineerToken;port=$NormalDebugPort+2}) }
    foreach ($auth in $authCases) {
        $preparedAuth = [string](& (Join-Path $PSScriptRoot 'simulate-station01-load.ps1') -PrepareOnly -OutputDirectory $root -ApiBase $api)
        $authProcess = Start-Desktop $auth.kind $auth.token $auth.port $preparedAuth
        & node (Join-Path $PSScriptRoot 'capture-station01-webview2-normal.cjs') $auth.port $root $auth.kind 1> (Join-Path $root "$($auth.kind)-capture.out.log") 2> (Join-Path $root "$($auth.kind)-capture.err.log")
        $authExit = $LASTEXITCODE
        Stop-Owned $authProcess.Id $desktop
        if ($authExit -ne 0) { throw "$($auth.kind) page capture failed: $authExit" }
    }
    $exitCode = 0
} catch {
    Save-Json 'validation-error.json' ([ordered]@{ atUtc=[datetime]::UtcNow.ToString('o'); phase=$phase; error=$_.Exception.ToString(); noAutomaticRetry=$true })
} finally {
    for ($index=$owned.Count-1; $index -ge 0; $index--) {
        $entry = $owned[$index]
        try { Stop-Owned $entry.pid $entry.expected }
        catch { Save-Json "cleanup-error-$($entry.kind).json" @{ error=$_.Exception.Message; pid=$entry.pid } }
    }
    Save-Json 'validation-result.json' ([ordered]@{ atUtc=[datetime]::UtcNow.ToString('o'); phase=$phase; exitCode=$exitCode;
        sessionId=(Get-Process -Id $PID).SessionId; ownedProcesses=@($owned); source='Test/VirtualPlc only'; realDeviceVerified=$false })
}
exit $exitCode
