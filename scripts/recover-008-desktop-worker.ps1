$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$control = Join-Path $repo 'artifacts/recipe-execution-008/desktop-worker-control'
$request = Get-Content (Join-Path $control 'recovery-request.json') -Raw | ConvertFrom-Json
$attempt = Join-Path $control ('attempt-' + [datetime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '-' + $PID)
New-Item -ItemType Directory -Path $attempt -ErrorAction Stop | Out-Null
try {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $session = (Get-Process -Id $PID).SessionId
    if ($session -eq 0 -or $identity.Name -ne $request.desktopAccount) { throw 'Administrator interactive desktop is required.' }
    $allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/recipe-execution-008')) + '\'
    $oldRoot = [IO.Path]::GetFullPath([string]$request.originalRoot)
    $nextRoot = [IO.Path]::GetFullPath([string]$request.preparedRoot)
    foreach ($path in @($oldRoot,$nextRoot)) {
        if (-not $path.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Recovery root outside 008 artifacts.' }
    }
    $workerScript = Join-Path $PSScriptRoot 'wait-008-page-batch.ps1'
    $processes = @(Get-CimInstance Win32_Process -ErrorAction Stop)
    $workers = @($processes | Where-Object { $_.Name -eq 'pwsh.exe' -and $_.CommandLine -and
        $_.CommandLine.Contains($workerScript,[StringComparison]::OrdinalIgnoreCase) })
    foreach ($worker in $workers) {
        foreach ($candidate in @($oldRoot,$nextRoot)) {
            $readyPath = Join-Path $candidate 'worker-ready.json'
            if (-not (Test-Path -LiteralPath $readyPath)) { continue }
            $ready = Get-Content $readyPath -Raw | ConvertFrom-Json
            $readyUtc = ([datetime]$ready.startedAtUtc).ToUniversalTime()
            $bornUtc = $worker.CreationDate.ToUniversalTime()
            if ($worker.ProcessId -eq $ready.pid -and $worker.SessionId -eq $session -and
                $ready.sessionId -eq $session -and $ready.user -eq $identity.Name -and
                $worker.CommandLine.Contains('"' + $candidate + '"',[StringComparison]::OrdinalIgnoreCase) -and
                $bornUtc -le $readyUtc -and ($readyUtc - $bornUtc).TotalSeconds -lt 30) {
                @{state='ReusedLiveWorker';ready=$ready;creationUtc=$bornUtc;atUtc=[datetime]::UtcNow.ToString('o')} |
                    ConvertTo-Json -Depth 8 | Set-Content (Join-Path $attempt 'result.json')
                Write-Output "Reused live worker $($ready.pid) in Session $session."
                exit 0
            }
        }
    }
    if ($workers.Count -gt 0) { throw 'An existing 008 worker could not be matched to the requested ready identity; it was not stopped.' }
    $oldRuns = Join-Path $oldRoot 'runs'
    $leftovers = @($processes | Where-Object { $_.CommandLine -and
        $_.CommandLine.Contains($oldRuns,[StringComparison]::OrdinalIgnoreCase) })
    if ($leftovers.Count -gt 0) {
        $leftovers | Select-Object ProcessId,ParentProcessId,CreationDate,SessionId,CommandLine |
            ConvertTo-Json -Depth 6 | Set-Content (Join-Path $attempt 'remaining-processes.json')
        throw 'Original batch still has processes; no conflicting worker was started.'
    }
    $ports = @(25132..25135)+@(25142..25145)+@(25152..25155)+@(25162..25165)+@(25182..25185)+@(25192..25195)
    $listeners = @(netstat -ano -p TCP | Select-String '\sLISTENING\s')
    foreach ($port in $ports) {
        if (@($listeners | Select-String ":$port\s").Count -gt 0) { throw "Port $port is occupied; no conflicting worker was started." }
    }
    $lockedLog = Join-Path $oldRoot 'queue/job-008.err.log'
    if (Test-Path -LiteralPath $lockedLog) {
        $stream = [IO.File]::Open($lockedLog,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
        $stream.Dispose()
    }
    if (-not (Test-Path (Join-Path $nextRoot 'queue/batch-paused.signal')) -or
        (Test-Path (Join-Path $nextRoot 'worker-ready.json')) -or
        (Test-Path (Join-Path $nextRoot 'worker-finished.json')) -or
        @(Get-ChildItem (Join-Path $nextRoot 'runs')).Count -gt 0) { throw 'Recovery requires a new, prepared and paused root.' }
    if ((Get-FileHash $workerScript).Hash -ne $request.workerSha256) { throw 'Prepared worker digest mismatch.' }
    @{state='OriginalResourcesReconciled';originalRoot=$oldRoot;sessionId=$session;account=$identity.Name
        noMatchingProcess=$true;portsFree=$true;redirectedLogUnlocked=$true;originalFailureRetained=$true
        atUtc=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json |
        Set-Content (Join-Path $attempt 'resource-reconciliation.json')
    $env:Path = 'C:\Program Files\dotnet;E:\nodejs;C:\Users\Administrator\AppData\Local\Programs\Python\Python312;' + $env:Path
    $worker = Start-Process -FilePath 'C:\Program Files\PowerShell\7\pwsh.exe' -WindowStyle Hidden -PassThru `
        -WorkingDirectory $repo -ArgumentList @('-NoProfile','-File',('"'+$workerScript+'"'),'-BatchRoot',('"'+$nextRoot+'"')) `
        -RedirectStandardOutput (Join-Path $attempt 'worker.out.log') -RedirectStandardError (Join-Path $attempt 'worker.err.log')
    $end = [datetime]::UtcNow.AddSeconds(30)
    while (-not (Test-Path (Join-Path $nextRoot 'worker-ready.json')) -and -not $worker.HasExited -and [datetime]::UtcNow -lt $end) {
        Start-Sleep -Milliseconds 200
    }
    $nextReady = Get-Content (Join-Path $nextRoot 'worker-ready.json') -Raw | ConvertFrom-Json
    if ($worker.HasExited -or $nextReady.pid -ne $worker.Id -or $nextReady.sessionId -ne $session -or
        $nextReady.user -ne $identity.Name -or $nextReady.scriptSha256 -ne $request.workerSha256) {
        throw 'Recovery worker did not produce matching live desktop ready; prepared queue stays paused.'
    }
    @{state='RecoveredPausedWorker';ready=$nextReady;originalRoot=$oldRoot;preparedRoot=$nextRoot
        atUtc=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $attempt 'result.json')
    $nextRoot | Set-Content (Join-Path $allowed 'current-night-batch.txt')
    Write-Output "worker ready: PID $($nextReady.pid), Session $session, paused for acceptance review."
} catch {
    @{state='RecoveryFailed';reason=$_.Exception.ToString();position=$_.InvocationInfo.PositionMessage
        atUtc=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $attempt 'result.json')
    throw
}
