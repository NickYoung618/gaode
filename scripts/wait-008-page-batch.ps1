param([Parameter(Mandatory)][string]$BatchRoot)
$ErrorActionPreference = 'Stop'
$workerPwsh = Join-Path $PSHOME 'pwsh.exe'
if (-not (Test-Path $workerPwsh)) { throw 'Run this worker using PowerShell 7.' }
foreach ($tool in @('dotnet','node','python')) { Get-Command $tool -ErrorAction Stop | Out-Null }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/recipe-execution-008'))
$root = [IO.Path]::GetFullPath($BatchRoot)
if (-not $root.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)) { throw 'BatchRoot outside 008 artifacts.' }
if ((Get-Process -Id $PID).SessionId -eq 0) { throw 'This worker requires the remote interactive desktop session.' }
if ((Test-Path -LiteralPath $root) -and
    ((Test-Path (Join-Path $root 'worker-ready.json')) -or
     (Test-Path (Join-Path $root 'worker-finished.json')) -or
     @(Get-ChildItem -LiteralPath (Join-Path $root 'runs') -ErrorAction SilentlyContinue).Count -gt 0)) {
    throw 'BatchRoot already has a worker or run evidence; choose a new root.'
}
New-Item -ItemType Directory -Path (Join-Path $root 'queue') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $root 'runs') -Force | Out-Null
$ready = @{ sessionId = (Get-Process -Id $PID).SessionId
    pid = $PID; scriptSha256 = (Get-FileHash $PSCommandPath).Hash
    user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    startedAtUtc = [datetime]::UtcNow.ToString('o'); state = 'WaitingForJobs' }
$ready | ConvertTo-Json | Set-Content (Join-Path $root 'worker-ready.json') -Encoding utf8
Write-Output "008 interactive worker ready in Session $($ready.sessionId). Keep this PowerShell window open."
$deadline = (Get-Date).AddHours(16)
while ((Get-Date) -lt $deadline) {
    if (Test-Path -LiteralPath (Join-Path $root 'queue/finish.signal')) { break }
    if (Test-Path -LiteralPath (Join-Path $root 'queue/batch-paused.signal')) {
        Start-Sleep -Seconds 2
        continue
    }
    $jobs = @(Get-ChildItem -LiteralPath (Join-Path $root 'queue') -Filter 'job-*.json' -File |
        Where-Object { $_.Name -match '^job-[0-9]{3}\.json$' } | Sort-Object Name)
    foreach ($jobFile in $jobs) {
        if ((Test-Path -LiteralPath (Join-Path $root 'queue/batch-paused.signal')) -or
            (Test-Path -LiteralPath (Join-Path $root 'queue/finish.signal'))) { break }
        $jobId = [IO.Path]::GetFileNameWithoutExtension($jobFile.Name)
        $done = Join-Path $root "queue/$jobId.result.json"
        if (Test-Path -LiteralPath $done) { continue }
        $caseId = $null; $caseRoot = $null; $exitCode = 1
        $runner = $null; $launchAttempted = $false; $timedOut = $false; $cleanupVerified = $false; $cleanupErrors = @()
        $tracked = @{}; $ownershipQueryFailed = $false; $reusedIdentities = @()
        try {
            $job = Get-Content -LiteralPath $jobFile.FullName -Raw | ConvertFrom-Json
            if ($job.reloadWorkerRoot) {
                $caseId = 'WORKER-RELOAD'
                $nextRoot = [IO.Path]::GetFullPath([string]$job.reloadWorkerRoot)
                if ($nextRoot -eq $root -or -not $nextRoot.StartsWith($allowed + [IO.Path]::DirectorySeparatorChar,
                    [StringComparison]::OrdinalIgnoreCase)) { throw 'Worker reload root outside new 008 batch.' }
                if ((Test-Path (Join-Path $nextRoot 'worker-ready.json')) -or
                    (Test-Path (Join-Path $nextRoot 'worker-finished.json')) -or
                    @(Get-ChildItem (Join-Path $nextRoot 'runs') -ErrorAction SilentlyContinue).Count -gt 0 -or
                    @(Get-ChildItem (Join-Path $nextRoot 'queue') -Filter 'job-*.json' -ErrorAction SilentlyContinue).Count -gt 0) {
                    throw 'Worker reload requires a new paused root with no jobs.'
                }
                if (-not (Test-Path (Join-Path $nextRoot 'queue/batch-paused.signal'))) {
                    throw 'Successor worker must remain paused until its frozen batch is prepared.'
                }
                $expectedHash = (Get-FileHash $PSCommandPath).Hash
                if ($job.scriptSha256 -ne $expectedHash) { throw 'Worker reload script digest mismatch.' }
                $successor = Start-Process -FilePath $workerPwsh -WindowStyle Hidden -PassThru `
                    -WorkingDirectory $repo -ArgumentList @('-NoProfile','-File', ('"' + $PSCommandPath + '"'),
                        '-BatchRoot', ('"' + $nextRoot + '"'))
                $reloadDeadline = [datetime]::UtcNow.AddSeconds(30)
                while (-not (Test-Path (Join-Path $nextRoot 'worker-ready.json')) -and
                    [datetime]::UtcNow -lt $reloadDeadline -and -not $successor.HasExited) { Start-Sleep -Milliseconds 200 }
                try {
                    $nextReady = Get-Content (Join-Path $nextRoot 'worker-ready.json') -Raw | ConvertFrom-Json
                    if ($nextReady.pid -ne $successor.Id -or $nextReady.sessionId -ne $ready.sessionId -or
                        $nextReady.user -ne $ready.user -or $nextReady.scriptSha256 -ne $expectedHash -or $successor.HasExited) {
                        throw 'Successor worker identity or loaded script mismatch.'
                    }
                } catch {
                    if (-not $successor.HasExited) { $successor.Kill(); $successor.WaitForExit(5000) | Out-Null }
                    throw
                }
                @{ successor = $nextReady; sourceWorker = $ready; formalWpfAttempt = $false } |
                    ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root "queue/$jobId.worker-reload.json") -Encoding utf8
                'Successor ready in the same interactive session.' | Set-Content (Join-Path $root 'queue/finish.signal')
                $exitCode = 0
                continue
            }
            $caseId = [string]$job.caseId
            if ($caseId -cnotmatch '^Q(0[1-9]|1[0-9]|2[0-2])$' -and
                $caseId -cnotin @('Q01-PARAM','Q03-NG','Q03-Pending','GROUP-F','GROUP-F-MIXED','GROUP-F-PENDING','GROUP-A-E',
                    'ASSEMBLY-A-E','ASSEMBLY-A-E-NOCODE','ASSEMBLY-A-E-ERROR','ASSEMBLY-A-E-NG','Q04-MANUAL',
                    'ROT-PART-OK','ROT-PART-NG','ROT-PART-PENDING','ROT-ASSEMBLY-OK','ROT-ASSEMBLY-NG','ROT-ASSEMBLY-PENDING',
                    'RECOVERY-3D','ASSEMBLY-A-E-MANUAL','ASSEMBLY-A-E-PENDING',
                    'Q01-NG','Q02-PENDING','Q02-PENDING-P03','RECOVERY-F','Q01-PAUSE')) {
                throw 'Only supported 008 page cases are accepted.'
            }
            $caseRoot = Join-Path $root "runs/$jobId-$caseId"
            if (Test-Path -LiteralPath $caseRoot) { throw 'Job evidence root already exists.' }
            $budgetArgs = @{CaseId=$caseId}
            if ($job.hostDll) { $budgetArgs.BinaryRoot = Split-Path ([IO.Path]::GetFullPath([string]$job.hostDll)) }
            $budget = & (Join-Path $repo 'scripts/get-008-page-budget.ps1') @budgetArgs
            if ($job.authorizationMode -and ($job.caseId -cne 'Q01' -or $job.authorizationMode -cnotin @('Auth401','Auth403') -or $job.preflightOnly)) { throw 'Unsupported authorization page job.' }
            if ($job.preflightOnly -or $job.authorizationMode) { $budget.jobTimeoutMs = 240000 }
            $started = @{ caseId = $caseId; startedAtUtc = [datetime]::UtcNow.ToString('o'); budget = $budget }
            $started | ConvertTo-Json | Set-Content (Join-Path $root "queue/$jobId.started.json") -Encoding utf8
            Write-Output "Starting $jobId $caseId"
            $runnerArguments = @('-NoProfile','-File', ('"' + (Join-Path $repo 'scripts/verify-q01-q02-test-page.ps1') + '"'),
                    '-EvidenceRoot', ('"' + $caseRoot + '"'), '-Cases', $caseId)
            if ($job.preflightOnly) { $runnerArguments += '-PreflightOnly' }
            if ($job.authorizationMode) { $runnerArguments += @('-AuthorizationMode', [string]$job.authorizationMode) }
            if ($job.hostDll) { $runnerArguments += @('-HostDll', ('"' + [string]$job.hostDll + '"')) }
            if ($job.plcDll) { $runnerArguments += @('-PlcDll', ('"' + [string]$job.plcDll + '"')) }
            if ($job.windowsNativeThreadPool) { $runnerArguments += '-WindowsNativeThreadPool' }
            if ($job.externalTestData) { $runnerArguments += '-ExternalTestData' }
            if ($job.hostSocketInlineCompletions) { $runnerArguments += '-HostSocketInlineCompletions' }
            if ($job.plcSocketInlineCompletions) { $runnerArguments += '-PlcSocketInlineCompletions' }
            $launchAttempted = $true
            $runner = Start-Process -FilePath $workerPwsh -WindowStyle Hidden -PassThru `
                -ArgumentList $runnerArguments `
                -WorkingDirectory $repo -RedirectStandardOutput (Join-Path $root "queue/$jobId.out.log") `
                -RedirectStandardError (Join-Path $root "queue/$jobId.err.log")
            $started.runnerPid = $runner.Id
            $started | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $root "queue/$jobId.started.json") -Encoding utf8
            $jobDeadline = [datetime]::UtcNow.AddMilliseconds($budget.jobTimeoutMs)
            do {
                try {
                    $processes = @(Get-CimInstance Win32_Process -ErrorAction Stop)
                    $parentCreated = @{}
                    if (-not $runner.HasExited) {
                        $runnerIdentity = @($processes | Where-Object { $_.ProcessId -eq $runner.Id })
                        if ($runnerIdentity.Count -eq 1) {
                            $parentCreated[[int]$runner.Id] = $runnerIdentity[0].CreationDate
                        } elseif (-not $runner.HasExited) { throw 'Live runner identity unavailable.' }
                    }
                    foreach ($identity in $tracked.Values) {
                        if (@($processes | Where-Object { $_.ProcessId -eq $identity.id -and
                            $_.CreationDate -eq $identity.created -and $_.ExecutablePath -eq $identity.path }).Count -eq 1) {
                            $parentCreated[[int]$identity.id] = $identity.created
                        }
                    }
                    do {
                        $added = $false
                        foreach ($process in $processes) {
                            # ParentProcessId survives parent exit and PID reuse. An older
                            # process cannot be a child of this newly created parent.
                            $identityKey = "$($process.ProcessId):$($process.CreationDate.ToUniversalTime().Ticks)"
                            if ($parentCreated.ContainsKey([int]$process.ParentProcessId) -and
                                $process.CreationDate -ge $parentCreated[[int]$process.ParentProcessId] -and
                                $process.SessionId -eq $ready.sessionId -and
                                -not $tracked.ContainsKey($identityKey)) {
                                $tracked[$identityKey] = @{ id = [int]$process.ProcessId
                                    parentId = [int]$process.ParentProcessId; created = $process.CreationDate
                                    path = $process.ExecutablePath }
                                $parentCreated[[int]$process.ProcessId] = $process.CreationDate; $added = $true
                            }
                        }
                    } while ($added)
                } catch { $ownershipQueryFailed = $true }
                if ($runner.WaitForExit(2000)) { break }
                if ([datetime]::UtcNow -ge $jobDeadline) {
                    $timedOut = $true
                    $runner.Kill($true) # only this Process object's descendants
                    $runner.WaitForExit(5000) | Out-Null
                    break
                }
            } while ($true)
            $exitCode = if ($timedOut) { 124 } else { $runner.ExitCode }
        } catch {
            # Start-Process can still hold its redirected stderr file after a partial launch.
            # Preserve the original failure without opening that same file for writing.
            @{ exception = $_.Exception.ToString(); errorId = $_.FullyQualifiedErrorId
                position = $_.InvocationInfo.PositionMessage; atUtc = [datetime]::UtcNow.ToString('o') } |
                ConvertTo-Json -Depth 6 | Set-Content (Join-Path $root "queue/$jobId.worker-error.json") -Encoding utf8
        } finally {
            if ($runner) {
                try {
                    if (-not $runner.HasExited) { $runner.Kill($true); $runner.WaitForExit(5000) | Out-Null }
                    foreach ($identity in $tracked.Values) {
                        $current = Get-CimInstance Win32_Process -Filter "ProcessId = $($identity.id)" -ErrorAction Stop
                        if (-not $current) { continue }
                        if ($current.CreationDate -ne $identity.created -or $current.ExecutablePath -ne $identity.path) {
                            # The recorded owned identity has exited. The new identity
                            # belongs to no cleanup authorization from this record.
                            $reusedIdentities += @{ id = $identity.id; recordedCreated = $identity.created
                                currentCreated = $current.CreationDate; disposition = 'NotStoppedDifferentIdentity' }
                            continue
                        }
                        try { Stop-Process -Id $identity.id -ErrorAction Stop } catch {
                            # A WebView2 child can exit between the identity query and Stop-Process.
                            # Only accept an already absent PID; all other failures still pause.
                            if (Get-Process -Id $identity.id -ErrorAction SilentlyContinue) { throw }
                        }
                        $process = Get-Process -Id $identity.id -ErrorAction SilentlyContinue
                        if ($process -and -not $process.WaitForExit(5000)) { throw "PID $($identity.id) did not exit." }
                    }
                    if ($ownershipQueryFailed) { throw 'Descendant ownership could not be verified during this job.' }
                    $ports = @(25132..25135) + @(25142..25145) + @(25152..25155) + @(25162..25165) + @(25182..25185) + @(25192..25195)
                    $listeners = @(netstat -ano -p TCP | Select-String '\sLISTENING\s')
                    foreach ($port in $ports) {
                        if (@($listeners | Select-String ":$port\s").Count -gt 0) { throw "Port $port still in use; no next job." }
                    }
                    $cleanupVerified = $true
                } catch { $cleanupErrors += $_.Exception.Message }
            } elseif ($launchAttempted) {
                $cleanupErrors += 'Runner launch attempted without a process identity; resource release requires reconciliation.'
            } else { $cleanupVerified = $true } # no process launch attempted
            if (-not $cleanupVerified) {
                $exitCode = 1
                'Resource release unknown; inspect cleanup evidence before removing this pause.' |
                    Set-Content (Join-Path $root 'queue/batch-paused.signal')
            }
            @{ verified = $cleanupVerified; errors = $cleanupErrors; timedOut = $timedOut
                identities = @($tracked.Values); reusedIdentities = $reusedIdentities
                atUtc = [datetime]::UtcNow.ToString('o') } |
                ConvertTo-Json -Depth 12 | Set-Content (Join-Path $root "queue/$jobId.cleanup.json") -Encoding utf8
            @{ caseId = $caseId; evidenceRoot = $caseRoot; exitCode = $exitCode
                timedOut = $timedOut; cleanupVerified = $cleanupVerified
                finishedAtUtc = [datetime]::UtcNow.ToString('o') } |
                ConvertTo-Json | Set-Content -LiteralPath $done -Encoding utf8
            Write-Output "Finished $jobId $caseId exitCode=$exitCode"
        }
        if (-not $cleanupVerified) { break }
    }
    if (Test-Path -LiteralPath (Join-Path $root 'queue/finish.signal')) { break }
    Start-Sleep -Seconds 2
}
@{ finishedAtUtc = [datetime]::UtcNow.ToString('o') } | ConvertTo-Json |
    Set-Content (Join-Path $root 'worker-finished.json') -Encoding utf8
