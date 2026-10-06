$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$control = Join-Path $repo 'artifacts/recipe-execution-008/desktop-worker-control'
$request = Get-Content (Join-Path $control 'recovery-request.json') -Raw | ConvertFrom-Json
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if ($identity.Name -ne $request.desktopAccount -or (Get-Process -Id $PID).SessionId -eq 0) {
    throw 'Run this setup in the Administrator desktop.'
}
$service = New-Object -ComObject 'Schedule.Service'
$service.Connect()
$rootFolder = $service.GetFolder('\')
$taskName = 'Gaode008DesktopRecovery'
try { $existing = $rootFolder.GetTask($taskName) } catch {
    # PowerShell wraps COM lookup errors in MethodInvocationException.
    $lookupError = $_.Exception
    while ($lookupError.InnerException) { $lookupError = $lookupError.InnerException }
    if ($lookupError.HResult -ne -2147024894) { throw }
    $existing = $null
}
if ($existing) { throw 'Recovery task already exists; inspect it rather than overwriting it.' }
$task = $service.NewTask(0)
$task.RegistrationInfo.Description = '008 only: reuse valid desktop worker, or start the prepared paused recovery root after resource reconciliation. No login trigger.'
$task.Principal.UserId = $identity.Name
$task.Principal.LogonType = 3 # InteractiveToken: no password and requires a logged-on desktop.
$task.Principal.RunLevel = 1
$task.Settings.Enabled = $true
$task.Settings.AllowDemandStart = $true
$task.Settings.MultipleInstances = 2 # IgnoreNew: never replace an active instance.
$task.Settings.ExecutionTimeLimit = 'PT0S'
$task.Settings.AllowHardTerminate = $false
$task.Settings.DisallowStartIfOnBatteries = $false
$task.Settings.StopIfGoingOnBatteries = $false
$action = $task.Actions.Create(0)
$action.Path = 'C:\Program Files\PowerShell\7\pwsh.exe'
$action.Arguments = '-NoProfile -WindowStyle Hidden -File "' + (Join-Path $PSScriptRoot 'recover-008-desktop-worker.ps1') + '"'
$action.WorkingDirectory = $repo
$desktopSid = $identity.User.Value
$codexSid = 'S-1-5-21-2233435179-746101852-1345246527-1003'
$sddl = 'D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;' + $desktopSid + ')(A;;FRFX;;;' + $codexSid + ')'
# CREATE only. Grant Codex read/execute, never task modification or deletion.
$registered = $rootFolder.RegisterTaskDefinition($taskName,$task,2,$identity.Name,$null,3,$sddl)
@{taskName=$taskName;desktopAccount=$identity.Name;codexSid=$codexSid;taskSddl=$registered.GetSecurityDescriptor(4)
    actionPath=$action.Path;actionArguments=$action.Arguments;logonType=3;triggerCount=$task.Triggers.Count
    purpose='On-demand desktop recovery only; login auto-start deferred';atUtc=[datetime]::UtcNow.ToString('o')} |
    ConvertTo-Json -Depth 6 | Set-Content (Join-Path $control 'task-grant.json')
Write-Output '008 desktop recovery permission ready. Codex can now query/run this task; no worker has been stopped or started by setup.'
