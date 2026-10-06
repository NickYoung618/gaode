#requires -Version 7.0
# Session 0 cannot initialize the elevated sandbox helper on this SSH host.
# This bridge uses the SAME logged-on user, LUA, without credentials or ACL changes.
[CmdletBinding(DefaultParameterSetName='Dispatch')]
param(
    [Parameter(ParameterSetName='Dispatch')][string]$Phase,
    [Parameter(ParameterSetName='Dispatch')][string]$ProbeFolder,
    [Parameter(Mandatory,ParameterSetName='Worker')][string]$Manifest
)
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$false
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
function Inside([string]$Path,[string]$Parent) {
    $full=[IO.Path]::GetFullPath($Path)
    $prefix=[IO.Path]::GetFullPath($Parent).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
    if(-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw "路径不在允许目录：$full" }
    return $full
}
function Save-NewJson([string]$Path,$Value) {
    if(Test-Path -LiteralPath $Path) { throw "已有执行产物，拒绝覆盖：$Path" }
    $temp=$Path+'.pending'
    $stream=[IO.File]::Open($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try {
        $bytes=[Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Depth 8))
        $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true)
    } finally { $stream.Dispose() }
    [IO.File]::Move($temp,$Path)
}
if($PSCmdlet.ParameterSetName -eq 'Worker') {
    $Manifest=Inside $Manifest $root
    $data=Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
    $area=Split-Path -Parent $Manifest
    $code=1
    try {
        if($data.root -ne $root -or $data.user -ne [Security.Principal.WindowsIdentity]::GetCurrent().Name) { throw '桥接用户或项目身份不匹配' }
        $session=(Get-Process -Id $PID).SessionId
        if($session -eq 0) { throw '桥接未进入用户登录会话，不允许回退到Session 0' }
        Save-NewJson (Join-Path $area 'worker-start.json') @{pid=$PID;session_id=$session;user=$data.user;nonce=$data.nonce;started=[datetime]::UtcNow.ToString('o')}
        # runner.context() requires the request JSON FILE, never its directory.
        if($data.probe_folder) { $env:GAODE_WORKFLOW_REQUEST=$null }
        else {
            if(-not $data.request_file) { throw '桥接清单缺少request_file，不接受旧目录字段' }
            $env:GAODE_WORKFLOW_REQUEST=Inside $data.request_file (Join-Path $root '.specify/workflows/requests')
        }
        $env:GAODE_WORKFLOW_PYTHON=$data.python
        $env:PYTHONUTF8='1'
        $env:PYTHONDONTWRITEBYTECODE='1'
        $arguments=@('-B',(Join-Path $PSScriptRoot 'prompt_stage.py'))
        if($data.probe_folder) { $arguments+=@('--probe-folder',(Inside $data.probe_folder (Join-Path $root 'artifacts/workflow-doctor'))) }
        else { $arguments+=@('--phase',$data.phase) }
        Set-Location -LiteralPath $root
        & $data.python @arguments 1> (Join-Path $area 'worker.stdout.log') 2> (Join-Path $area 'worker.stderr.log')
        $code=$LASTEXITCODE
    } catch {
        $_ | Out-String | Set-Content -LiteralPath (Join-Path $area 'bridge-error.log') -Encoding utf8
    } finally {
        Save-NewJson (Join-Path $area 'worker-result.json') @{exit_code=$code;nonce=$data.nonce;finished=[datetime]::UtcNow.ToString('o')}
    }
    exit $code
}

if(-not $env:GAODE_WORKFLOW_PYTHON) { throw '缺少Workflow Python路径' }
if($ProbeFolder) {
    $ProbeFolder=Inside $ProbeFolder (Join-Path $root 'artifacts/workflow-doctor')
    $expected=Get-Content -LiteralPath (Join-Path $ProbeFolder 'expected.json') -Raw | ConvertFrom-Json
    $folder=$ProbeFolder
    $Phase='probe'
    # Both capacity attempts share the worker's 180-second deadline.
    $limit=220
} else {
    if($Phase -notin @('specify','plan','tasks','analyze','implement','review','fix')) { throw '阶段无效' }
    $requestFile=Inside $env:GAODE_WORKFLOW_REQUEST (Join-Path $root '.specify/workflows/requests')
    if(-not (Test-Path -LiteralPath $requestFile -PathType Leaf) -or [IO.Path]::GetFileName($requestFile) -ne 'request.json') { throw 'GAODE_WORKFLOW_REQUEST必须指向现有request.json文件' }
    $folder=Split-Path -Parent $requestFile
    $request=Get-Content -LiteralPath $requestFile -Raw | ConvertFrom-Json
    $control=Get-Content -LiteralPath (Join-Path $folder 'control.json') -Raw | ConvertFrom-Json
    $expected=$control.current
    if($request.request_id -ne (Split-Path -Leaf $folder) -or $expected.request_id -ne $request.request_id) { throw '桥接请求身份不匹配' }
    if($expected.phase -ne $Phase) { throw '桥接阶段不匹配' }
    $limit=7000
}
if($expected.nonce -notmatch '^[a-f0-9]{32}$') { throw '桥接nonce格式无效' }
$area=Join-Path $folder ('execution-logs/bridge-'+$expected.nonce)
New-Item -ItemType Directory -Path $area -ErrorAction Stop | Out-Null
$manifestPath=Join-Path $area 'manifest.json'
$user=[Security.Principal.WindowsIdentity]::GetCurrent().Name
Save-NewJson $manifestPath @{root=$root;user=$user;python=$env:GAODE_WORKFLOW_PYTHON;request_file=$(if($ProbeFolder){$null}else{$requestFile});probe_folder=$ProbeFolder;phase=$Phase;nonce=$expected.nonce}
$taskName='GaodeWorkflowStage-'+$expected.nonce
$scheduler=New-Object -ComObject Schedule.Service
$scheduler.Connect()
$schedulerRoot=$scheduler.GetFolder('\')
$definition=$scheduler.NewTask(0)
$definition.RegistrationInfo.Description='Temporary Gaode Workflow stage; same-user interactive token, limited privileges'
$definition.Principal.UserId=$user
$definition.Principal.LogonType=3 # TASK_LOGON_INTERACTIVE_TOKEN; no stored password
$definition.Principal.RunLevel=0 # TASK_RUNLEVEL_LUA; never highest privileges
$definition.Settings.ExecutionTimeLimit='PT'+($limit+10)+'S'
$definition.Settings.Hidden=$true
$definition.Settings.DisallowStartIfOnBatteries=$false
$definition.Settings.StopIfGoingOnBatteries=$false
$action=$definition.Actions.Create(0)
$action.Path=(Get-Command pwsh -ErrorAction Stop).Source
$action.Arguments='-NoProfile -NonInteractive -WindowStyle Hidden -File "'+$PSCommandPath+'" -Manifest "'+$manifestPath+'"'
$action.WorkingDirectory=$root
$registered=$false
$task=$null
$code=1
try {
    $task=$schedulerRoot.RegisterTaskDefinition($taskName,$definition,2,$null,$null,3,$null) # CREATE only
    $registered=$true
    $instance=$task.Run($null)
    Save-NewJson (Join-Path $area 'dispatch.json') @{task_name=$taskName;instance_id=$instance.InstanceGuid;user=$user;run_level='LUA';logon_type='InteractiveToken';nonce=$expected.nonce;timeout_seconds=$limit}
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $receiptSeenAt=$null
    $settled=$false
    $lastProgress=-20
    do {
        Start-Sleep -Milliseconds 300
        $task=$schedulerRoot.GetTask($taskName)
        $running=$task.GetInstances(0).Count -gt 0
        $finished=Test-Path -LiteralPath (Join-Path $area 'worker-result.json')
        if($timer.Elapsed.TotalSeconds -ge ($lastProgress + 20) -and -not $finished) {
            $lastProgress=[math]::Floor($timer.Elapsed.TotalSeconds/20)*20
            [Console]::Error.WriteLine(("[Workflow进度] phase={0}; elapsed={1}s; worker logs={2}" -f $Phase,$lastProgress,$area))
        }
        if($timer.Elapsed.TotalSeconds -gt 30 -and -not (Test-Path -LiteralPath (Join-Path $area 'worker-start.json'))) { throw '用户登录会话未启动Worker；需要同一用户保持登录，SSH Session 0不可替代' }
        # GetInstances can be empty between Run() returning and the action actually starting.
        # Require the nonce-bound Worker receipt before treating an empty collection as completion.
        if($finished) {
            if($null -eq $receiptSeenAt) { $receiptSeenAt=$timer.Elapsed.TotalSeconds }
            $result=Get-Content -LiteralPath (Join-Path $area 'worker-result.json') -Raw | ConvertFrom-Json
            if($result.nonce -ne $expected.nonce) { throw 'Worker回执nonce不匹配' }
            $code=[int]$result.exit_code
            # The worker writes its receipt just before exiting. Scheduler's last-result
            # publication can lag; require convergence, never treat the receipt alone as exit.
            $settled=(-not $running -and $task.State -notin @(2,4) -and $task.LastTaskResult -eq $code)
            if(-not $settled -and $timer.Elapsed.TotalSeconds-$receiptSeenAt -gt 10) {
                Save-NewJson (Join-Path $area 'dispatch-mismatch.json') @{worker_exit_code=$code;task_result=$task.LastTaskResult;task_state=$task.State;instances_running=$running;nonce=$expected.nonce}
                throw 'Task Scheduler与Worker退出结果未在10秒内收敛'
            }
        }
    } while(-not $settled -and $timer.Elapsed.TotalSeconds -lt $limit)
    if(-not $settled) { throw '阶段桥接到期或缺少可靠退出回执；不自动重试或新建运行' }
    Save-NewJson (Join-Path $area 'dispatch-result.json') @{exit_code=$code;task_result=$task.LastTaskResult;nonce=$expected.nonce;elapsed_seconds=$timer.Elapsed.TotalSeconds}
    if($code -eq 0) { Get-Content -LiteralPath (Join-Path $area 'worker.stdout.log') -Raw }
    else {
        $outcome=Get-ChildItem -LiteralPath (Join-Path $folder 'execution-logs') -Filter ($Phase+'-'+$expected.nonce+'*.execution.json') |
            Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        $failure='worker_failure'
        if($outcome) {
            $execution=Get-Content -LiteralPath $outcome.FullName -Raw -Encoding utf8 | ConvertFrom-Json
            if($execution.failure_kind) { $failure=$execution.failure_kind }
        }
        throw "阶段Worker失败 [$failure]，查看 $area"
    }
} finally {
    if($registered) {
        # Stop queued as well as running instances before deleting this exact temporary task.
        $task.Enabled=$false
        try { $task.Stop(0) } catch { if($_.Exception.HResult -ne -2147216629) { throw } }
        $schedulerRoot.DeleteTask($taskName,0)
    }
}
exit $code
