param(
    [ValidateSet('recipe','manual')][string]$View = 'manual',
    [switch]$NoBrowser
)
$ErrorActionPreference = 'Stop'
$plcReleaseRoot = 'D:\Gaode-PlcCommissioning-20261006\release'
$plcCandidates = @(Get-ChildItem -LiteralPath $plcReleaseRoot -Directory | ForEach-Object {
    if ($_.Name -match '^Gaode-PlcCommissioning-(\d+\.\d+\.\d+)-win-x64$') {
        $plcVersion = [version]$Matches[1]
        if ((Test-Path -LiteralPath (Join-Path $_.FullName 'release.ready.json')) -and (Test-Path -LiteralPath (Join-Path $_.FullName 'runtime\python.exe')) -and (Test-Path -LiteralPath (Join-Path $_.FullName 'Start-PLC.ps1'))) {
            try {
                $plcReadyInfo = Get-Content -LiteralPath (Join-Path $_.FullName 'release.ready.json') -Raw | ConvertFrom-Json
                if ($plcReadyInfo.ready -eq $true -and [version]$plcReadyInfo.version -eq $plcVersion) { [PSCustomObject]@{Version=$plcVersion;Root=$_.FullName} }
            } catch { }
        }
    }
} | Sort-Object Version -Descending)
if ($plcCandidates.Count -eq 0) { throw '未找到已发布的Windows x64完整包，请查看release目录。' }
$plcPackageRoot = $plcCandidates[0].Root
Write-Host ('使用最新联调包：' + $plcPackageRoot)
& (Join-Path $plcPackageRoot 'runtime\python.exe') -X utf8 (Join-Path $plcPackageRoot 'tools\activate_release.py') --target $plcPackageRoot
if ($LASTEXITCODE -ne 0) { throw '未切换联调包，请按上方提示处理；未启动运动。' }
& (Join-Path $plcPackageRoot 'Start-PLC.ps1') -View $View -NoBrowser:$NoBrowser
