$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$evidence = Join-Path $repo 'specs/021-commissioning-console/evidence'
$session = Join-Path ([IO.Path]::GetTempPath()) ('gaode-021-probe-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($session) | Out-Null
$operatorCredential = [Guid]::NewGuid().ToString('N')
$engineerCredential = [Guid]::NewGuid().ToString('N')
$env:GAODE_OFFLINE_OPERATOR_CREDENTIAL = $operatorCredential
$env:GAODE_OFFLINE_ENGINEER_CREDENTIAL = $engineerCredential
$fixtureExe = Join-Path $PSScriptRoot 'bin/Debug/net10.0/Gaode.CommissioningUiFixture.exe'
$probeExe = Join-Path $repo 'desktop/tests/Gaode.Desktop.CommissioningProbe/bin/Debug/net10.0-windows10.0.17763.0/Gaode.Desktop.CommissioningProbe.exe'
$manifestPath = Join-Path $session 'manifest.json'
$fixture = Start-Process -FilePath $fixtureExe -ArgumentList @('--mode','commissioning','--output',$manifestPath) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $session 'fixture.out') -RedirectStandardError (Join-Path $session 'fixture.err')
$results = @()
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while (-not (Test-Path -LiteralPath $manifestPath)) {
        if ($fixture.HasExited) { throw 'Offline fixture exited before manifest; inspect local fixture.err' }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Offline fixture startup timeout' }
        Start-Sleep -Milliseconds 100
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $env:GAODE_MODE = 'RealDeviceCommissioning'
    $env:GAODE_API_BASE_URL = $manifest.apiBaseUrl
    $env:GAODE_SIGNALR_URL = $manifest.signalRUrl
    $env:GAODE_FRONTEND_DIST = Join-Path $repo 'frontend/dist'
    foreach ($name in @('operator','engineer')) {
        $env:GAODE_COMMISSIONING_PROFILE_PATH = if ($name -eq 'operator') { $manifest.operatorProfile } else { $manifest.engineerProfile }
        # The desktop inherits only its selected credential; never the other role's secret.
        $env:GAODE_OFFLINE_OPERATOR_CREDENTIAL = if ($name -eq 'operator') { $operatorCredential } else { $null }
        $env:GAODE_OFFLINE_ENGINEER_CREDENTIAL = if ($name -eq 'engineer') { $engineerCredential } else { $null }
        $role = if ($name -eq 'operator') { 'L1' } else { 'L2' }
        $scenario = [ordered]@{scope='OFFLINE';timeoutSeconds=25;outputPath=(Join-Path $session ($name + '-result.json'));steps=@(
            @{id='confirmed-user';waitUntil=$true;script="document.getElementById('user')?.value === 'OFFLINE $name'"},
            @{id='role-mismatch-rejected';script="document.querySelectorAll('.role-chip').forEach(c=>c.classList.toggle('active',c.dataset.role==='L3')); window.handleLogin({preventDefault(){}}) === false && !!document.getElementById('user').validationMessage";expectedJson='true'},
            @{id='accepted-login';script="document.querySelectorAll('.role-chip').forEach(c=>c.classList.toggle('active',c.dataset.role==='$role')); window.handleLogin({preventDefault(){}})";expectedJson='true'},
            @{id='formal-running-page';waitUntil=$true;script="location.pathname==='/prototype.html' && !!window.station01"},
            @{id='no-query-or-global-config';script="location.search==='' && window.__GAODE_HOST_CONFIG__===undefined";expectedJson='true'},
            @{id='no-persistent-credential';script="![localStorage,sessionStorage].some(s=>Object.keys(s).some(k=>/token|password|credential/i.test(k)))";expectedJson='true'},
            @{id='no-frame-host-config';script="(()=>{const f=document.createElement('iframe');document.body.appendChild(f);return f.contentWindow.__GAODE_HOST_CONFIG__===undefined})()";expectedJson='true'},
            @{id='recipe-permission';script="document.getElementById('btnRecipe').disabled === " + $(if ($name -eq 'operator') {'true'} else {'false'});expectedJson='true'}
        )}
        $scenarioPath = Join-Path $session ($name + '-scenario.json')
        $scenario | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $scenarioPath -Encoding utf8
        $probe = Start-Process -FilePath $probeExe -ArgumentList @('--scenario',$scenarioPath) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $session ($name + '.out')) -RedirectStandardError (Join-Path $session ($name + '.err'))
        if (-not $probe.WaitForExit(35000)) { Stop-Process -Id $probe.Id; $results += @{role=$name;state='Blocked';reason='ProbeTimeout'}; continue }
        if (Test-Path -LiteralPath $scenario.outputPath) { $results += Get-Content -LiteralPath $scenario.outputPath -Raw | ConvertFrom-Json }
        else { $results += @{role=$name;state='Blocked';reason='NoProbeReport';exitCode=$probe.ExitCode} }
    }
    foreach ($reject in @('invalid','missing')) {
        $env:GAODE_COMMISSIONING_PROFILE_PATH = $manifest.operatorProfile
        $env:GAODE_OFFLINE_ENGINEER_CREDENTIAL = $null
        $env:GAODE_OFFLINE_OPERATOR_CREDENTIAL = if ($reject -eq 'invalid') { [Guid]::NewGuid().ToString('N') } else { $null }
        $scenario = @{scope='OFFLINE';timeoutSeconds=15;outputPath=(Join-Path $session ($reject + '-result.json'));steps=@()}
        if ($reject -eq 'missing') { $scenario.expectedInitializationError = 'InvalidOperationException' }
        else { $scenario.steps = @(@{id='invalid-identity-refused';waitUntil=$true;script="!!document.getElementById('user')?.validationMessage && window.station01===undefined"}) }
        $scenarioPath = Join-Path $session ($reject + '-scenario.json')
        $scenario | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $scenarioPath -Encoding utf8
        $probe = Start-Process -FilePath $probeExe -ArgumentList @('--scenario',$scenarioPath) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $session ($reject + '.out')) -RedirectStandardError (Join-Path $session ($reject + '.err'))
        if (-not $probe.WaitForExit(25000)) { Stop-Process -Id $probe.Id; $results += @{case=$reject;state='Blocked';reason='ProbeTimeout'} }
        elseif (Test-Path -LiteralPath $scenario.outputPath) { $results += Get-Content -LiteralPath $scenario.outputPath -Raw | ConvertFrom-Json }
        else { $results += @{case=$reject;state='Blocked';reason='NoProbeReport';exitCode=$probe.ExitCode} }
    }
    if (-not $fixture.HasExited) { Stop-Process -Id $fixture.Id; $fixture.WaitForExit() }
    $secretFound = $false
    foreach ($file in @(Get-ChildItem -LiteralPath $session -File) + @(Get-ChildItem -LiteralPath (Join-Path $manifest.root 'desktop-logs') -File -ErrorAction SilentlyContinue)) {
        $text = [IO.File]::ReadAllText($file.FullName)
        if ($text.Contains($operatorCredential) -or $text.Contains($engineerCredential)) { $secretFound = $true }
    }
    [ordered]@{scope='OFFLINE:actual-desktop-identity-only';checkedAtUtc=[DateTime]::UtcNow.ToString('o');results=$results;credentialFoundInReportsOrDesktopLogs=$secretFound;localSession=$session;fieldValidation='T055/T056 Blocked'} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $evidence 'v01-identity.json') -Encoding utf8
    if (Test-Path -LiteralPath (Join-Path $manifest.root 'desktop-logs/desktop-runtime.jsonl')) {
        Copy-Item -LiteralPath (Join-Path $manifest.root 'desktop-logs/desktop-runtime.jsonl') -Destination (Join-Path $evidence 'v01-desktop-runtime-complete.jsonl')
    }
    Write-Output ($results | ConvertTo-Json -Depth 6 -Compress)
} finally {
    if (-not $fixture.HasExited) { Stop-Process -Id $fixture.Id }
    $env:GAODE_OFFLINE_OPERATOR_CREDENTIAL=$null; $env:GAODE_OFFLINE_ENGINEER_CREDENTIAL=$null
}
