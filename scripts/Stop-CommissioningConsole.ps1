[CmdletBinding(SupportsShouldProcess)]
param([string]$InstallationRoot=(Split-Path $PSScriptRoot))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($InstallationRoot)
$targets=@(
    (Join-Path $root 'app/desktop/Gaode.Station01.Desktop.exe'),
    (Join-Path $root 'app/host/Gaode.Host.exe'),
    (Join-Path $root 'app/worker/Gaode.CameraWorker.exe')
)
$logPath=Join-Path $root 'data/close-history.jsonl'
function Record-Close($kind,$facts){
    if(-not $WhatIfPreference -and (Test-Path -LiteralPath (Join-Path $root 'data'))){
        @{utc=[DateTimeOffset]::UtcNow;kind=$kind;facts=$facts}|ConvertTo-Json -Compress -Depth 4|Add-Content -LiteralPath $logPath -Encoding utf8
    }
}
Write-Output '关闭本版本软件；这不是设备急停，也不会发送PLC复位或运动指令。'
Record-Close 'Closing' @{installationRoot=$root}
foreach($path in $targets){
    $matches=@(Get-CimInstance Win32_Process -Filter ("Name='"+[IO.Path]::GetFileName($path)+"'")|Where-Object {$_.ExecutablePath -and [string]::Equals($_.ExecutablePath,$path,[StringComparison]::OrdinalIgnoreCase)})
    foreach($item in $matches){
        if(-not $PSCmdlet.ShouldProcess(($item.Name+' PID='+$item.ProcessId+' '+$path),'关闭进程')){continue}
        # Recheck the executable immediately before acting; do not trust saved PIDs.
        $current=Get-CimInstance Win32_Process -Filter ('ProcessId='+$item.ProcessId)
        if(-not $current -or -not [string]::Equals($current.ExecutablePath,$path,[StringComparison]::OrdinalIgnoreCase)){continue}
        $process=Get-Process -Id $item.ProcessId -ErrorAction SilentlyContinue
        if(-not $process){continue}
        if($item.Name -eq 'Gaode.Station01.Desktop.exe'){
            if($process.CloseMainWindow()){$null=$process.WaitForExit(2000)}
        }
        $process.Refresh()
        if(-not $process.HasExited){
            Stop-Process -InputObject $process -Force -ErrorAction Stop
            if(-not $process.WaitForExit(5000)){throw ('进程未退出：'+$item.Name+' PID='+$item.ProcessId)}
        }
        Record-Close 'ProcessClosed' @{name=$item.Name;processId=$item.ProcessId;path=$path}
        Write-Output ('已关闭 '+$item.Name+' PID='+$item.ProcessId)
    }
}
if($WhatIfPreference){Write-Output '仅列出目标，没有关闭程序。';return}
$remaining=@(Get-CimInstance Win32_Process|Where-Object {$_.ExecutablePath -and $targets -contains $_.ExecutablePath})
Record-Close 'Completed' @{remaining=$remaining.Count;physicalStopVerified=$false;automaticReplay=$false}
if($remaining.Count){throw '本版本仍有进程未退出，请查看data/close-history.jsonl。'}
Write-Output '本版本桌面、后台及相机进程已全部关闭。可重新双击“启动联调.cmd”。'
