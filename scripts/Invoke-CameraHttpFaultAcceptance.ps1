param(
    [string]$HostPath = 'D:\gaode\artifacts\gaode-camera-019-review-a0e70e3\host\Gaode.Host.exe',
    [string]$WorkerPath = 'D:\gaode\backend\tests\Gaode.CameraWorkerFixture\bin\Debug\net10.0\Gaode.CameraWorkerFixture.exe',
    [Parameter(Mandatory)][string]$DataRoot,
    [int]$Port = 5197
)
$ErrorActionPreference = 'Stop'
foreach ($path in @($HostPath, $WorkerPath, $DataRoot)) {
    if (![IO.Path]::IsPathFullyQualified($path)) { throw 'Absolute paths required' }
}
if ((Split-Path $WorkerPath -Leaf) -ne 'Gaode.CameraWorkerFixture.exe') { throw 'Offline fixture required' }
if (Test-Path -LiteralPath $DataRoot) { throw 'Use a new isolated directory' }
foreach ($path in @($HostPath, $WorkerPath)) {
    if (!(Test-Path -LiteralPath $path)) { throw "Missing executable: $path" }
}
if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) { throw 'Port in use' }
New-Item -ItemType Directory -Path $DataRoot | Out-Null
$store = Join-Path $DataRoot 'store'
$sdk = Join-Path $DataRoot 'no-sdk'
New-Item -ItemType Directory -Path $sdk | Out-Null
@{ Cameras = @(@{ Role='A'; Kind='2D'; Serial='offline-http-fixture'; ExpectedNicMac='001122334455' }) } |
    ConvertTo-Json -Depth 5 | Set-Content (Join-Path $DataRoot 'site.json') -Encoding utf8
$token = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$headers = @{ Authorization="Bearer $token" }
$config = @{ Urls="http://127.0.0.1:$Port"; Gaode=@{
    Mode='Production'; CaptureOnly=$true; CameraStoreRoot=$store
    Tokens=@{ Operator=([Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))); SystemAdministrator=$token }
    Cameras=@{ SitePath=(Join-Path $DataRoot 'site.json'); WorkerPath=$WorkerPath
        GalaxySdkPath=$sdk; CameraProSdkPath=$sdk; StateRoot=(Join-Path $DataRoot 'state')
        StartupTimeoutMs=5000; CaptureTimeoutMs=5000; ShutdownTimeoutMs=3000 }
} }
$config | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $DataRoot 'appsettings.json') -Encoding utf8
& $HostPath --prepare-camera-store $store
if ($LASTEXITCODE) { throw 'Prepare isolated database failed' }
$base = "http://127.0.0.1:$Port"
$checks = [Collections.Generic.List[object]]::new()
function Request([string]$Method, [string]$Path) {
    $response = Invoke-WebRequest -Uri "$base$Path" -Method $Method -Headers $headers -SkipHttpErrorCheck -TimeoutSec 15
    $body = $response.Content | ConvertFrom-Json
    $checks.Add(@{ utc=[DateTimeOffset]::UtcNow; method=$Method; path=$Path; status=[int]$response.StatusCode; body=$body })
    return @{ Code=[int]$response.StatusCode; Body=$body }
}
function Require([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message } }
$process = $null
$passed = $false
try {
    # The product executable is unchanged. Only its content root/configuration is isolated.
    $process = Start-Process -FilePath $HostPath -ArgumentList @('--contentRoot', ('"' + $DataRoot + '"')) -WorkingDirectory $DataRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $DataRoot 'host.stdout.log') -RedirectStandardError (Join-Path $DataRoot 'host.stderr.log')
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
    do {
        if ($process.HasExited) { throw 'Host exited during startup; inspect isolated logs' }
        try { $initial = Request GET '/api/v1/cameras'; break } catch [System.Net.Http.HttpRequestException] { Start-Sleep -Milliseconds 100 }
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    Require ($null -ne $initial -and $initial.Code -eq 200) 'Host HTTP startup failed'
    $ready = @($initial.Body.cameras)[0]
    Require ($ready.state -eq 'Ready' -and @($initial.Body.cameras).Count -eq 1) 'Expected exactly one ready offline camera'
    $listeners = @(Get-NetTCPConnection -OwningProcess $process.Id -State Listen)
    Require ($listeners.Count -eq 1 -and $listeners[0].LocalAddress -eq '127.0.0.1' -and $listeners[0].LocalPort -eq $Port) 'Host must listen only on selected loopback address'
    $modules = @(Get-Process -Id $ready.processId -Module | Select-Object -ExpandProperty ModuleName)
    Require (@($modules | Where-Object { $_ -match '^(GxIAPI|GxIAPINET|CameraPro)\.' }).Count -eq 0) 'Vendor SDK unexpectedly loaded'
    $healthy = Request POST '/api/v1/cameras/A/recover'
    Require ($healthy.Code -eq 409 -and $healthy.Body.status.sessionId -eq $ready.sessionId -and $healthy.Body.status.processId -eq $ready.processId) 'Healthy recovery must reject without replacement'
    Set-Content (Join-Path $DataRoot 'exit') 'exit17'
    $log = Join-Path $DataRoot 'state/A.host.jsonl'
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(5)
    do { Start-Sleep -Milliseconds 50 } while (!(Select-String -LiteralPath $log -Pattern 'CameraWorkerExitedUnexpectedly' -Quiet) -and [DateTimeOffset]::UtcNow -lt $deadline)
    Require ([bool](Select-String -LiteralPath $log -Pattern 'CameraWorkerExitedUnexpectedly' -Quiet)) 'Exited observer did not record fault'
    $fault = Request GET '/api/v1/cameras'
    $faulted = @($fault.Body.cameras)[0]
    Require ($faulted.state -eq 'Faulted' -and $null -eq $faulted.processId -and $faulted.maxBytes -eq 0 -and $faulted.error) 'Exit must revoke readiness'
    $rejected = Request POST '/api/v1/cameras/A/captures'
    Require ($rejected.Code -eq 409 -and $rejected.Body.error -eq 'CameraNotReady') 'Offline capture must reject via HTTP'
    Remove-Item -LiteralPath (Join-Path $DataRoot 'exit')
    Set-Content (Join-Path $DataRoot 'mode.txt') 'init-fail' -NoNewline
    $failed = Request POST '/api/v1/cameras/A/recover'
    Require ($failed.Code -eq 503 -and $failed.Body.error -eq 'CameraRecoveryFailed' -and $failed.Body.status.state -eq 'Faulted' -and !$failed.Body.automaticReplay) 'Failed recovery must return HTTP503'
    Set-Content (Join-Path $DataRoot 'mode.txt') 'normal' -NoNewline
    $recovered = Request POST '/api/v1/cameras/A/recover'
    Require ($recovered.Code -eq 200 -and $recovered.Body.state -eq 'Ready' -and $recovered.Body.sessionId -ne $ready.sessionId) 'Explicit recovery must establish a new ready session'
    $media = Request GET '/api/v1/camera-media'
    Require (@($media.Body).Count -eq 0) 'Fault test must not publish media'
    Require (!(Test-Path -LiteralPath (Join-Path $DataRoot 'triggers.txt'))) 'Rejected/unknown requests must not trigger or replay'
    $passed = $true
} finally {
    if ($process -and !$process.HasExited) {
        $stop = Request POST '/api/v1/cameras/shutdown'
        Require ($stop.Code -eq 202) 'Normal shutdown rejected'
        Require ($process.WaitForExit(15000)) 'Host did not exit normally; do not freeze this root'
    }
    @{ passed=$passed; kind='offline-fixture-formal-host-http'; realHardwareValidated=$false
        binarySourceRevision='a0e70e3d89d8078d916d9134d7e9b2f3cbd0a56b'
        hostPath=$HostPath; hostSha256=(Get-FileHash $HostPath).Hash
        workerPath=$WorkerPath; workerSha256=(Get-FileHash $WorkerPath).Hash
        listeners=$listeners | Select-Object LocalAddress,LocalPort; workerModules=$modules
        hostStopped=(!$process -or $process.HasExited); checks=$checks } |
        ConvertTo-Json -Depth 18 | Set-Content (Join-Path $DataRoot 'http-fault-checks.json') -Encoding utf8
}
Write-Output "Formal HTTP fault acceptance passed; stopped evidence: $DataRoot"
