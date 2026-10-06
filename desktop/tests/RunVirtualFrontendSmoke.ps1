param([string]$EvidenceRoot = '')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if (-not $EvidenceRoot) { $EvidenceRoot = Join-Path $repo "artifacts/frontend/007/runtime-$stamp" }
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$testRoot = Join-Path $repo "artifacts/station01-007/frontend-006-$stamp"
New-Item -ItemType Directory -Path $EvidenceRoot -Force | Out-Null
$env:GAODE_TEST_OPERATOR_TOKEN = [guid]::NewGuid().ToString('N')
$env:Gaode__Tokens__ProcessEngineer = [guid]::NewGuid().ToString('N')
$env:GAODE_API_BASE_URL = 'http://127.0.0.1:5001'
$env:GAODE_SIGNALR_URL = 'http://127.0.0.1:5001/hubs/station01'
$env:GAODE_MODE = 'Test'
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = '--remote-debugging-port=9222'
$desktop = $null
$record = $null

function Invoke-Cdp([System.Net.WebSockets.ClientWebSocket]$socket, [int]$id, [string]$expression) {
    $payload = @{ id = $id; method = 'Runtime.evaluate'; params = @{
        expression = $expression; returnByValue = $true; awaitPromise = $true
    } } | ConvertTo-Json -Compress -Depth 6
    $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
    $socket.SendAsync([ArraySegment[byte]]::new($bytes),
        [System.Net.WebSockets.WebSocketMessageType]::Text, $true,
        [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    while ($true) {
        $buffer = New-Object byte[] 1048576
        $result = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer),
            [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        $message = [Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count) | ConvertFrom-Json
        if ($message.id -eq $id) {
            if ($message.result.exceptionDetails) { throw $message.result.exceptionDetails.text }
            return $message.result.result.value
        }
    }
}

try {
    if (Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
        Where-Object LocalPort -in 5001, 5080, 9222) {
        throw 'Host/VirtualPlc/CDP port is occupied; no process was stopped or reset.'
    }
    $recordPath = @(& (Join-Path $repo 'scripts/start-station01-virtual-loop.ps1') `
        -SkipDesktop -TestRoot $testRoot -OperatorToken $env:GAODE_TEST_OPERATOR_TOKEN)[-1]
    $record = Get-Content -Raw $recordPath | ConvertFrom-Json
    $loadPath = @(& (Join-Path $repo 'scripts/simulate-station01-load.ps1') `
        -PrepareOnly -OutputDirectory $EvidenceRoot -Scenario S1 -OccupiedSlots P01)[-1]
    $env:GAODE_TEST_PREPARED_LOAD_PATH = [IO.Path]::GetFullPath($loadPath)
    $prepared = Get-Content -Raw $env:GAODE_TEST_PREPARED_LOAD_PATH | ConvertFrom-Json
    $desktopExe = Join-Path $repo 'desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe'
    $desktop = Start-Process -FilePath $desktopExe -WorkingDirectory (Split-Path $desktopExe) `
        -WindowStyle Hidden -PassThru
    $target = $null
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 500
        try { $target = @(Invoke-RestMethod 'http://127.0.0.1:9222/json/list' |
            Where-Object { $_.type -eq 'page' -and $_.url -like 'https://appassets.local/*' })[0] } catch { }
        if ($target) { break }
        if ($desktop.HasExited) { throw 'WPF desktop exited before WebView2 page became available.' }
    }
    if (-not $target) { throw 'Actual WebView2 page was not exposed on the controlled CDP port.' }
    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $socket.ConnectAsync([uri]$target.webSocketDebuggerUrl,
        [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    try {
        Invoke-Cdp $socket 1 "document.getElementById('loginForm').requestSubmit(); true" | Out-Null
        for ($i = 0; $i -lt 20; $i++) {
            Start-Sleep -Milliseconds 250
            $page = Invoke-Cdp $socket (10 + $i) 'location.pathname'
            if ($page -eq '/prototype.html') { break }
        }
        if ($page -ne '/prototype.html') { throw "Login navigation did not reach approved page: $page" }
        Invoke-Cdp $socket 40 @'
window.__gaodeSmoke={commands:[],renders:[],notifications:[],errors:[],connected:0};
window.addEventListener('station01:command',e=>window.__gaodeSmoke.commands.push(e.detail));
window.addEventListener('station01:rendered',e=>window.__gaodeSmoke.renders.push(e.detail));
window.addEventListener('station01:notification',e=>window.__gaodeSmoke.notifications.push(e.detail));
window.addEventListener('station01:error',e=>window.__gaodeSmoke.errors.push(e.detail));
window.addEventListener('station01:connected',()=>window.__gaodeSmoke.connected++);
true
'@ | Out-Null
        $clicked = Invoke-Cdp $socket 41 "Array.from(document.querySelectorAll('button')).find(x=>x.textContent.includes('启动')).click(); true"
        if (-not $clicked) { throw 'Existing start button was not clicked.' }
        $observed = $null
        for ($i = 0; $i -lt 40; $i++) {
            Start-Sleep -Milliseconds 500
            $observed = Invoke-Cdp $socket (50 + $i) 'JSON.stringify(window.__gaodeSmoke)'
            if (($observed | ConvertFrom-Json).commands.Count -gt 0) { break }
        }
        $smoke = $observed | ConvertFrom-Json
        if ($smoke.commands.Count -ne 1 -or -not $smoke.commands[0].runId) {
            throw 'Actual page did not produce one authorized StartRun receipt.'
        }
        $runId = $smoke.commands[0].runId
        $headers = @{ Authorization = "Bearer $env:GAODE_TEST_OPERATOR_TOKEN" }
        $anonymousStatus = 0
        try { Invoke-WebRequest "$env:GAODE_API_BASE_URL/api/v1/station01/status" -SkipHttpErrorCheck |
            ForEach-Object { $anonymousStatus = [int]$_.StatusCode } } catch { }
        $forbiddenStatus = 0
        $forbidden = Invoke-WebRequest "$env:GAODE_API_BASE_URL/api/v1/station01/runs" `
            -Method Post -Headers @{ Authorization = "Bearer $env:Gaode__Tokens__ProcessEngineer" } `
            -ContentType 'application/json' -Body ($prepared.request | ConvertTo-Json -Depth 8) -SkipHttpErrorCheck
        $forbiddenStatus = [int]$forbidden.StatusCode
        $run = Invoke-RestMethod "$env:GAODE_API_BASE_URL/api/v1/station01/runs/$runId" -Headers $headers
        $status = Invoke-RestMethod "$env:GAODE_API_BASE_URL/api/v1/station01/status" -Headers $headers
        $receiptRecord = [ordered]@{
            page = 'WPF/WebView2'; origin = 'https://appassets.local';
            request = $prepared.request; receipt = $smoke.commands[0];
            acceptedIsNotCompletion = ($run.wholeTaskState -ne 'FinalUnloadCompletion');
            authorizedGet = $true; anonymousGetStatus = $anonymousStatus;
            forbiddenStartStatus = $forbiddenStatus; runId = $runId
        }
        $receiptRecord | ConvertTo-Json -Depth 12 |
            Set-Content -Encoding utf8 (Join-Path $EvidenceRoot 'start-request.json')
        $deadline = (Get-Date).AddMinutes(3)
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 2
            $run = Invoke-RestMethod "$env:GAODE_API_BASE_URL/api/v1/station01/runs/$runId" -Headers $headers
            if ($run.wholeTaskState -eq 'FinalUnloadCompletion' -or $run.finalOutcome -eq 'Failed') { break }
        }
        Start-Sleep -Seconds 3
        $evidence = Invoke-RestMethod "$env:GAODE_API_BASE_URL/api/v1/station01/runs/$runId/evidence" -Headers $headers
        $pageState = Invoke-Cdp $socket 200 @'
JSON.stringify({url:location.href,verdict:document.getElementById('verdictBig')?.textContent,
  faults:document.getElementById('faultList')?.textContent,
  stages:Array.from(document.querySelectorAll('#moduleGrid > div')).map(x=>x.textContent.trim()),
  smoke:window.__gaodeSmoke})
'@
        [ordered]@{ runId = $runId; page = ($pageState | ConvertFrom-Json);
            backend = @{ wholeTaskState = $run.wholeTaskState; finalResult = $evidence.finalResult;
                stageEvents = @($evidence.stages | Select-Object stage,eventType);
                finalSource = $evidence.finalSourceMatrix.scope } } | ConvertTo-Json -Depth 14 |
            Set-Content -Encoding utf8 (Join-Path $EvidenceRoot 'status-mapping.json')
        [ordered]@{ desktopPid = $desktop.Id; pageUrl = $target.url; origin = 'https://appassets.local';
            hostPid = $record.hostPid; workerPid = $record.workerPid; plcPid = $record.plcPid;
            apiBase = $env:GAODE_API_BASE_URL; testRoot = $testRoot; runId = $runId;
            anonymousGetStatus = $anonymousStatus; forbiddenStartStatus = $forbiddenStatus;
            authorizedStart = $true; authorizedGet = $true;
            notificationCount = @($smoke.notifications).Count;
            pageFinal = (($pageState | ConvertFrom-Json).verdict -eq '完成');
            backendFinal = ($evidence.finalResult -eq 'FinalUnloadCompletion');
            prototypeSha256 = (Get-FileHash 'E:\dzk\gaode\原型.zip' -Algorithm SHA256).Hash } |
            ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 (Join-Path $EvidenceRoot 'webview2-host-transcript.json')
    } finally { $socket.Dispose() }
    Write-Output $EvidenceRoot
} finally {
    if ($desktop -and -not $desktop.HasExited) { Stop-Process -Id $desktop.Id -ErrorAction SilentlyContinue }
    if ($record) {
        foreach ($ownedId in @($record.watchPid, $record.hostPid, $record.workerPid, $record.plcPid)) {
            if ($ownedId -and (Get-Process -Id $ownedId -ErrorAction SilentlyContinue)) {
                Stop-Process -Id $ownedId -ErrorAction SilentlyContinue
            }
        }
    }
    Remove-Item Env:GAODE_TEST_OPERATOR_TOKEN -ErrorAction SilentlyContinue
    Remove-Item Env:Gaode__Tokens__ProcessEngineer -ErrorAction SilentlyContinue
}
