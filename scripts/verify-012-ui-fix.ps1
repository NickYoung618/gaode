[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable, [string[]]$Arguments = @(), [Parameter(Mandatory)][string]$Name)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if($workspace -ne 'E:\dzk\gaode-012-ui-fix'){throw 'Independent 012 workspace required'}
$evidence=Join-Path $workspace 'artifacts/recipe-ui-fix-012/verification'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
function Test-013Idle {
    $path='E:/dzk/gaode-1/.specify/workflows/verification.lock'
    $stream=$null
    try {
        $stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
        $null=$stream.ReadByte() # Read-only probe: another process' byte lock denies the read.
        return -not (Test-Path -LiteralPath ($path+'.json'))
    } catch { return $false } finally { if($stream){$stream.Dispose()} }
}
$record=[ordered]@{name=$Name;startedUtc=[DateTimeOffset]::UtcNow.ToString('o');executable=$Executable;arguments=$Arguments;workspace=$workspace;coordination='Read-only 013 verification-byte-lock and owner probe';status='NotStarted'}
$recordPath=Join-Path $evidence ($Name+'.json')
if(Test-Path -LiteralPath $recordPath){throw 'Evidence names must be unique'}
if(-not (Test-013Idle)){$record.status='Deferred013Busy';$record|ConvertTo-Json -Depth 5|Set-Content $recordPath;throw '013 is validating; no verification process started'}
$process=Start-Process -FilePath $Executable -ArgumentList $Arguments -WorkingDirectory $workspace -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $evidence ($Name+'.out.log')) -RedirectStandardError (Join-Path $evidence ($Name+'.err.log'))
$record.processId=$process.Id
$aborted=$false
while(-not $process.HasExited){
    if(-not (Test-013Idle)){
        # Terminate only this exact owned process tree, never any 013 process.
        $process.Kill($true)
        $aborted=$true;break
    }
    Start-Sleep -Milliseconds 500;$process.Refresh()
}
$process.WaitForExit();$record.endedUtc=[DateTimeOffset]::UtcNow.ToString('o');$record.exitCode=$process.ExitCode
$record.status=if($aborted){'Aborted013Window'}elseif($process.ExitCode -eq 0){'Passed'}else{'Failed'}
$record|ConvertTo-Json -Depth 5|Set-Content $recordPath
Write-Output "$Name $($record.status) exit=$($process.ExitCode)"
if($aborted -or $process.ExitCode -ne 0){exit 1}
