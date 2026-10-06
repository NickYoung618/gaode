param(
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [Parameter(Mandatory)][string]$ApiBase,
    [Parameter(Mandatory)][string]$PreparedLoadPath,
    [Parameter(Mandatory)][string]$OperatorToken,
    [int]$DebugPort = 9223
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$root = [IO.Path]::GetFullPath($EvidenceRoot)
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/station01-007'))
if (-not $root.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)) { throw 'EvidenceRoot outside isolated Test evidence.' }
$exe = Join-Path $repo 'desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe'
$dist = Join-Path $repo 'frontend/dist'
if (-not (Test-Path $exe) -or -not (Test-Path $dist)) { throw 'Build desktop and frontend first.' }
$env:GAODE_MODE = 'Test'
$env:GAODE_API_BASE_URL = $ApiBase
$env:GAODE_SIGNALR_URL = "$ApiBase/hubs/station01"
$env:GAODE_TEST_OPERATOR_TOKEN = $OperatorToken
$env:GAODE_TEST_PREPARED_LOAD_PATH = $PreparedLoadPath
$env:GAODE_FRONTEND_DIST = $dist
$env:WEBVIEW2_USER_DATA_FOLDER = Join-Path $root 'webview2-profile'
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort"
$env:GAODE_TEST_WEBVIEW2_DEBUG_PORT = [string]$DebugPort
$process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -WindowStyle Normal -PassThru
Start-Sleep -Seconds 3
$actual = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
[ordered]@{
    atUtc = [datetime]::UtcNow.ToString('o')
    processId = $process.Id
    sessionId = $actual.SessionId
    mainWindowHandle = if ($actual) { [string]$actual.MainWindowHandle } else { 'Exited' }
    desktopExeSha256 = (Get-FileHash $exe -Algorithm SHA256).Hash
    frontendRuntimeSha256 = (Get-FileHash (Join-Path $dist 'runtime.js') -Algorithm SHA256).Hash
    preparedLoadSha256 = (Get-FileHash $PreparedLoadPath -Algorithm SHA256).Hash
    apiBase = $ApiBase
    debugPort = $DebugPort
    source = 'Test/WPF-WebView2'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'interactive-desktop.json') -Encoding utf8
