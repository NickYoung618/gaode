$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) -or
    (Get-Process -Id $PID).SessionId -eq 0) {
    throw 'Run this reviewed setup script in the Administrator interactive desktop.'
}
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$service = New-Object -ComObject Schedule.Service
$service.Connect()
$folder = $service.GetFolder('\')
$task = $folder.GetTask('Gaode008DesktopRecovery')
$definition = $task.Definition
$taskUserSid = [Security.Principal.NTAccount]::new($definition.Principal.UserId).
    Translate([Security.Principal.SecurityIdentifier]).Value
$expectedArguments = '-NoProfile -WindowStyle Hidden -File "' +
    (Join-Path $PSScriptRoot 'recover-008-desktop-worker.ps1') + '"'
if ($definition.Actions.Count -ne 1 -or
    $definition.Actions.Item(1).Path -ne 'C:\Program Files\PowerShell\7\pwsh.exe' -or
    $definition.Actions.Item(1).Arguments -ne $expectedArguments -or
    $definition.Principal.LogonType -ne 3 -or
    $taskUserSid -ne $identity.User.Value) {
    throw 'Existing recovery task action/account differs from the reviewed Administrator task.'
}
if ($definition.Triggers.Count -ne 0) {
    throw 'Task already has a trigger; inspect it before changing the existing setup.'
}
$record = Join-Path $repo ('artifacts/recipe-execution-008/desktop-worker-control/login-config-' +
    [datetime]::UtcNow.ToString('yyyyMMddTHHmmssfff'))
New-Item -ItemType Directory $record | Out-Null
$task.Xml | Set-Content (Join-Path $record 'task-before.xml')
$securityDescriptor = $task.GetSecurityDescriptor(7)
$trigger = $definition.Triggers.Create(9)
$trigger.UserId = $identity.User.Value
$trigger.Enabled = $true
$definition.RegistrationInfo.Description =
    '008 only: at Administrator logon reuse a valid desktop worker, or start the prepared paused recovery root after resource reconciliation.'
# Keep the existing action, InteractiveToken principal and task ACL. No password is stored.
$updated = $folder.RegisterTaskDefinition('Gaode008DesktopRecovery', $definition, 22,
    $definition.Principal.UserId, $null, 3, $securityDescriptor)
$updated.Xml | Set-Content (Join-Path $record 'task-after.xml')
@{ taskName=$updated.Name; triggers=$updated.Definition.Triggers.Count;
    actions=$updated.Definition.Actions.Count; atUtc=[datetime]::UtcNow.ToString('o');
    mode='Administrator logon; reuse existing worker or recover paused worker; no job launch' } |
    ConvertTo-Json | Set-Content (Join-Path $record 'result.json')
Write-Output "008 recovery logon trigger configured. Review evidence: $record"
