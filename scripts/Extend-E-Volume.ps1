#requires -Version 5.1
param([switch]$ElevationAttempted)
$ErrorActionPreference = 'Stop'
Write-Host '[0] 扩展脚本已启动，正在检查管理员权限。' -ForegroundColor Cyan
$extensionLog = Join-Path $env:TEMP ('Gaode-E-Extension-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $PID + '.log')
$extensionTranscriptStarted = $false
try {
    Start-Transcript -Path $extensionLog -ErrorAction Stop | Out-Null
    $extensionTranscriptStarted = $true
    Write-Host ('运行日志：' + $extensionLog)
} catch { Write-Host ('日志未能创建：' + $_.Exception.Message) -ForegroundColor Yellow }
$extensionIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$extensionPrincipal = [Security.Principal.WindowsPrincipal]::new($extensionIdentity)
if (-not $extensionPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    if ($ElevationAttempted) {
        Write-Host 'Windows 未授予管理员权限，已停止。请使用管理员账户运行此脚本。' -ForegroundColor Red
        if ($extensionTranscriptStarted) { Stop-Transcript | Out-Null }
        exit 1
    }
    try {
        Write-Host '[1] 当前不是管理员，正在请求 Windows 管理员授权。' -ForegroundColor Yellow
        Write-Host '请查看任务栏或切换窗口，完成管理员授权/输入管理员凭据。'
        Write-Host '授权后会打开另一个窗口执行检查；本窗口等待该窗口结束。'
        $extensionElevated = Start-Process -FilePath ($env:SystemRoot + '\System32\WindowsPowerShell\v1.0\powershell.exe') -Verb RunAs -WindowStyle Normal -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $PSCommandPath + '"'), '-ElevationAttempted') -Wait -PassThru -ErrorAction Stop
        Write-Host ('管理员窗口已结束，退出码：' + $extensionElevated.ExitCode)
        if ($extensionTranscriptStarted) { Stop-Transcript | Out-Null }
        exit $extensionElevated.ExitCode
    } catch {
        Write-Host ('管理员授权未完成：' + $_.Exception.Message) -ForegroundColor Red
        if ($extensionTranscriptStarted) { Stop-Transcript | Out-Null }
        [void](Read-Host '按回车关闭')
        exit 1
    }
}
$extensionExitCode = 0
try {
    Write-Host '[1] 管理员权限已确认。' -ForegroundColor Green
    Write-Host '[2] 正在让 Windows 刷新磁盘容量信息。'
    Write-Host '若长时间没有下一行，请记录当前步骤和日志路径；不要重复运行。'
    Update-HostStorageCache -ErrorAction Stop
    Write-Host '[3] 刷新完成，正在查询 E 盘分区。'
    $extensionPartition = Get-Partition -DriveLetter E -ErrorAction Stop
    if (@($extensionPartition).Count -ne 1) { throw 'E: must identify exactly one partition.' }
    if ($extensionPartition.IsBoot -or $extensionPartition.IsSystem) { throw 'Refusing to extend a boot/system partition with this data-volume script.' }
    Write-Host ('[4] 正在查询 E 盘所在的数据磁盘，磁盘编号：' + $extensionPartition.DiskNumber)
    $extensionDisk = Get-Disk -Number $extensionPartition.DiskNumber -ErrorAction Stop
    if ($extensionDisk.Size -gt 201GB) { throw 'Backing disk is larger than the requested 200 GB data disk. Verify disk identity before resizing.' }
    Write-Host '[5] 正在检查 E 盘文件系统和健康状态。'
    $extensionVolume = Get-Volume -DriveLetter E -ErrorAction Stop
    if ($extensionDisk.IsOffline -or $extensionDisk.IsReadOnly) { throw 'The backing disk is offline or read-only.' }
    if ($extensionVolume.FileSystem -ne 'NTFS') { throw 'This script is restricted to the existing NTFS data volume.' }
    if ($extensionVolume.HealthStatus -ne 'Healthy') { throw 'The volume is not healthy; no resize was attempted.' }
    Write-Host '[6] 正在查询 E 盘可扩展到的最大容量（尚未修改分区）。'
    $extensionLimits = Get-PartitionSupportedSize -DiskNumber $extensionPartition.DiskNumber -PartitionNumber $extensionPartition.PartitionNumber -ErrorAction Stop
    [pscustomobject]@{
        Drive = 'E:'
        DiskNumber = $extensionDisk.Number
        DiskGiB = [math]::Round($extensionDisk.Size / 1GB, 3)
        PartitionGiB = [math]::Round($extensionPartition.Size / 1GB, 3)
        MaximumPartitionGiB = [math]::Round($extensionLimits.SizeMax / 1GB, 3)
        FreeGiB = [math]::Round($extensionVolume.SizeRemaining / 1GB, 3)
    } | Format-List | Out-Host
    if ($extensionLimits.SizeMax -le $extensionPartition.Size) {
        throw 'Windows reports no extendable space after E:. Check whether the data-disk expansion is visible and unallocated space is adjacent to E:. No changes made.'
    }
    Write-Host '[7] 现在开始扩展 E 盘。扩展执行期间请勿关闭此窗口。' -ForegroundColor Yellow
    Resize-Partition -DiskNumber $extensionPartition.DiskNumber -PartitionNumber $extensionPartition.PartitionNumber -Size $extensionLimits.SizeMax -ErrorAction Stop
    Write-Host '[8] 扩展命令已返回，正在核验结果。'
    $extensionAfter = Get-Partition -DriveLetter E -ErrorAction Stop
    if ($extensionAfter.Size -le $extensionPartition.Size) { throw 'The partition did not grow; verification failed.' }
    $extensionVolumeAfter = Get-Volume -DriveLetter E -ErrorAction Stop
    Write-Host '[9] 成功：E 盘分区已扩大，下面是最新容量。' -ForegroundColor Green
    [pscustomobject]@{
        Drive = 'E:'
        TotalGiB = [math]::Round($extensionVolumeAfter.Size / 1GB, 3)
        FreeGiB = [math]::Round($extensionVolumeAfter.SizeRemaining / 1GB, 3)
    } | Format-List | Out-Host
} catch {
    $extensionExitCode = 1
    Write-Host ('执行失败：' + $_.Exception.Message) -ForegroundColor Red
}
Write-Host ('请保留日志：' + $extensionLog)
if ($extensionTranscriptStarted) { Stop-Transcript | Out-Null }
[void](Read-Host '按回车关闭')
exit $extensionExitCode
