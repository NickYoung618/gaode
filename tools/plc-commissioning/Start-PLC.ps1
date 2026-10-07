param(
    [ValidateSet('recipe','manual')][string]$View = 'recipe',
    [switch]$NoBrowser
)
$ErrorActionPreference = 'Stop'
$plcRoot = $PSScriptRoot
$plcExpectedVersion = '1.1.6'
$plcUrl = 'http://127.0.0.1:18770'
$plcPage = if ($View -eq 'recipe') { $plcUrl + '/recipe' } else { $plcUrl + '/' }
$plcPython = Join-Path $plcRoot 'runtime\python.exe'
$plcEntry = Join-Path $plcRoot 'src\app.py'
if (-not (Test-Path -LiteralPath $plcPython)) { throw '缺少内置Windows x64运行环境，请完整解压联调ZIP。' }
$plcState = $null
try { $plcState = Invoke-RestMethod -Uri "$plcUrl/api/state" -TimeoutSec 2 } catch {}
if ($plcState) {
    if ($plcState.app -ne 'Gaode Independent PLC Commissioning' -or $plcState.rootPath -ne $plcRoot -or $plcState.version -ne $plcExpectedVersion) { throw '18770端口已有另一目录的服务，请先停止该服务。本包没有修改它。' }
    if (-not $NoBrowser) { Start-Process $plcPage }
    Write-Host ('联调界面已就绪：' + $plcPage + '；连接及发送由页面点击操作。')
    exit 0
}
& $plcPython -X utf8 (Join-Path $plcRoot 'tools\initialize_config.py')
if ($LASTEXITCODE -ne 0) { throw '本机配置初始化失败，请查看上方错误。' }
$plcLogs = Join-Path $plcRoot 'logs'
New-Item -ItemType Directory -Path $plcLogs -Force | Out-Null
$plcProcess = Start-Process -FilePath $plcPython -ArgumentList @('-X', 'utf8', ('"' + $plcEntry + '"'), '--port', '18770', '--no-browser') -WorkingDirectory $plcRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $plcLogs 'launcher-output.log') -RedirectStandardError (Join-Path $plcLogs 'launcher-error.log') -PassThru
$plcReady = $false
for ($plcAttempt = 0; $plcAttempt -lt 50; $plcAttempt++) {
    Start-Sleep -Milliseconds 200
    try {
        $plcState = Invoke-RestMethod -Uri "$plcUrl/api/state" -TimeoutSec 1
        if ($plcState.app -eq 'Gaode Independent PLC Commissioning' -and $plcState.rootPath -eq $plcRoot) { $plcReady = $true; break }
    } catch {}
    if ($plcProcess.HasExited) { break }
}
if (-not $plcReady) { throw '启动失败。请查看logs\launcher-error.log或startup-error.log；可能端口已被占用。' }
if (-not $NoBrowser) { Start-Process $plcPage }
Write-Host '联调台已启动，默认只读且未连接设备。填写PLC IP和端口后点击连接。'
