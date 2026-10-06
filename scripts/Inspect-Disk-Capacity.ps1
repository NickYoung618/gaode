#requires -Version 5.1
$ErrorActionPreference = 'Stop'
Write-Host 'Read-only disk capacity inspection. Checking administrator access...'
$capacityPrincipal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $capacityPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host 'Please approve the Windows administrator prompt. Results appear in a new window.' -ForegroundColor Yellow
    try {
        $capacityChild = Start-Process -FilePath ($env:SystemRoot + '\System32\WindowsPowerShell\v1.0\powershell.exe') -Verb RunAs -WindowStyle Normal -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $PSCommandPath + '"')) -Wait -PassThru -ErrorAction Stop
        exit $capacityChild.ExitCode
    } catch {
        Write-Host $_.Exception.Message -ForegroundColor Red
        [void](Read-Host 'Press Enter to close')
        exit 1
    }
}
$capacityReport = Join-Path $PSScriptRoot ('Disk-Capacity-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt')
$capacityTranscriptStarted = $false
$capacityExitCode = 0
try {
    Start-Transcript -Path $capacityReport | Out-Null
    $capacityTranscriptStarted = $true
    Write-Host ('Report: ' + $capacityReport)
    Write-Host '[1/2] Reading ALL physical disks. DiskGiB is the whole disk, not a drive-letter volume.' -ForegroundColor Cyan
    Get-Disk -ErrorAction Stop | Sort-Object Number | Select-Object Number,FriendlyName,SerialNumber,UniqueId,BusType,PartitionStyle,IsOffline,IsReadOnly,@{n='DiskGiB';e={[math]::Round($_.Size/1GB,3)}},@{n='AllocatedGiB';e={[math]::Round($_.AllocatedSize/1GB,3)}},@{n='UnallocatedGiB';e={[math]::Round(($_.Size-$_.AllocatedSize)/1GB,3)}},@{n='LargestFreeExtentGiB';e={[math]::Round($_.LargestFreeExtent/1GB,3)}} | Format-List | Out-Host
    Write-Host '[2/2] Reading partition-to-drive mappings.' -ForegroundColor Cyan
    Get-Partition -ErrorAction Stop | Sort-Object DiskNumber,PartitionNumber | Select-Object DiskNumber,PartitionNumber,DriveLetter,Type,IsBoot,IsSystem,@{n='PartitionGiB';e={[math]::Round($_.Size/1GB,3)}},Offset | Format-Table -AutoSize | Out-Host
    Write-Host 'Inspection complete. Send the report contents to the assistant.' -ForegroundColor Green
} catch {
    $capacityExitCode = 1
    Write-Host ('Inspection failed: ' + $_.Exception.Message) -ForegroundColor Red
} finally {
    if ($capacityTranscriptStarted) { Stop-Transcript | Out-Null }
}
Write-Host ('Report: ' + $capacityReport)
[void](Read-Host 'Press Enter to close')
exit $capacityExitCode
