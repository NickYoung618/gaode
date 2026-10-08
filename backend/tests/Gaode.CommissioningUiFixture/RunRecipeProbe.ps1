$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$evidence = Join-Path $repo ('specs/021-commissioning-console/evidence/recipe-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$session = Join-Path ([IO.Path]::GetTempPath()) ('gaode-021-probe-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($session) | Out-Null
$operatorCredential = [Guid]::NewGuid().ToString('N')
$engineerCredential = [Guid]::NewGuid().ToString('N')
$env:GAODE_OFFLINE_OPERATOR_CREDENTIAL = $operatorCredential
$env:GAODE_OFFLINE_ENGINEER_CREDENTIAL = $engineerCredential
$fixtureExe = Join-Path $PSScriptRoot 'bin/Debug/net10.0/Gaode.CommissioningUiFixture.exe'
$probeExe = Join-Path $repo 'desktop/tests/Gaode.Desktop.CommissioningProbe/bin/Debug/net10.0-windows10.0.17763.0/Gaode.Desktop.CommissioningProbe.exe'
$manifestPath = Join-Path $session 'manifest.json'
$env:GAODE_OFFLINE_VERIFY_RECIPE_EDIT_021 = '1'
$fixture = Start-Process -FilePath $fixtureExe -ArgumentList @('--mode','commissioning','--output',$manifestPath) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $session 'fixture.out') -RedirectStandardError (Join-Path $session 'fixture.err')
$results = @()
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while (-not (Test-Path -LiteralPath $manifestPath)) {
        if ($fixture.HasExited) { throw 'Offline fixture exited before manifest' }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Offline fixture startup timeout' }
        Start-Sleep -Milliseconds 100
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $deadline=[DateTime]::UtcNow.AddSeconds(10)
    while(-not(Test-Path -LiteralPath (Join-Path $manifest.root 'recipe-a-frozen.json'))){
        if([DateTime]::UtcNow -gt $deadline){throw 'Run A did not freeze before page editing'}
        Start-Sleep -Milliseconds 100
    }
    $env:GAODE_MODE='RealDeviceCommissioning'; $env:GAODE_API_BASE_URL=$manifest.apiBaseUrl; $env:GAODE_SIGNALR_URL=$manifest.signalRUrl
    $env:GAODE_FRONTEND_DIST=Join-Path $repo 'frontend/dist'; $env:GAODE_COMMISSIONING_PROFILE_PATH=$manifest.engineerProfile
    $env:GAODE_OFFLINE_OPERATOR_CREDENTIAL=$null
    $scenario = @{scope='OFFLINE';timeoutSeconds=30;outputPath=(Join-Path $session 'recipe-result.json');steps=@(
        @{id='engineer-confirmed';waitUntil=$true;script="document.getElementById('user')?.value==='OFFLINE engineer'"},
        @{id='login';script="window.handleLogin({preventDefault(){}})";expectedJson='true'},
        @{id='formal-page';waitUntil=$true;script="document.readyState==='complete' && location.pathname==='/prototype.html' && !!window.station01"},
        @{id='open-recipe';script="document.getElementById('btnRecipe').click(); true";expectedJson='true'},
        @{id='catalog';waitUntil=$true;script="document.getElementById('recipeAuthoringCatalog')?.options.length>1"},
        @{id='load-existing';script="(()=>{const s=document.getElementById('recipeAuthoringCatalog');s.value=s.options[1].value;s.dispatchEvent(new Event('change',{bubbles:true}));return true})()";expectedJson='true'},
        @{id='complete-body-loaded';waitUntil=$true;script="document.getElementById('recipeAuthoringNotice').textContent.includes('已读取保存内容')"},
        @{id='points-tab';script="document.querySelector('[data-authoring-section=points]').click();Array.from(document.querySelectorAll('.recipe-nav-button')).find(b=>b.textContent.includes('第1检测面')).click();true";expectedJson='true'},
        @{id='camera-fields';waitUntil=$true;script="!!document.querySelector('.recipe-capture-fields input[aria-label=`"曝光 (µs)`"]')"},
        @{id='edit-coordinate';script="(()=>{const c=document.querySelector('.recipe-capture-fields').parentElement;const e=c.querySelector('input[aria-label=`"X (mm)`"]');window.__OFFLINE_EDIT_EXPECTED__={x:Number(e.value)+1};e.value=String(window.__OFFLINE_EDIT_EXPECTED__.x);e.dispatchEvent(new Event('change',{bubbles:true}));return true})()";expectedJson='true'},
        @{id='edit-one-exposure';script="(()=>{const e=document.querySelector('.recipe-capture-fields input[aria-label=`"曝光 (µs)`"]');window.__OFFLINE_EDIT_EXPECTED__.exposure=Number(e.value)+100;e.value=String(window.__OFFLINE_EDIT_EXPECTED__.exposure);e.dispatchEvent(new Event('change',{bubbles:true}));return true})()";expectedJson='true'},
        @{id='save';script="document.getElementById('recipeAuthoringSave').click();true";expectedJson='true'},
        @{id='saved-and-reread';waitUntil=$true;script="document.getElementById('recipeAuthoringNotice').textContent.includes('已保存并重读')"},
        @{id='close-reopen';script="closeRecipe();document.getElementById('btnRecipe').click();true";expectedJson='true'},
        @{id='catalog-reloaded';waitUntil=$true;script="document.getElementById('recipeAuthoringCatalog')?.options.length>1"},
        @{id='read-saved-body-again';script="(()=>{const s=document.getElementById('recipeAuthoringCatalog');s.value=s.options[1].value;s.dispatchEvent(new Event('change',{bubbles:true}));return true})()";expectedJson='true'},
        @{id='reopened-full-body';waitUntil=$true;script="document.getElementById('recipeAuthoringNotice').textContent.includes('已读取保存内容')"},
        @{id='show-saved-points';script="document.querySelector('[data-authoring-section=points]').click();Array.from(document.querySelectorAll('.recipe-nav-button')).find(b=>b.textContent.includes('第1检测面')).click();true";expectedJson='true'},
        @{id='persisted-coordinate-and-exposure';script="(()=>{const c=document.querySelector('.recipe-capture-fields').parentElement;return Number(c.querySelector('input[aria-label=`"X (mm)`"]').value)===window.__OFFLINE_EDIT_EXPECTED__.x && Number(c.querySelector('input[aria-label=`"曝光 (µs)`"]').value)===window.__OFFLINE_EDIT_EXPECTED__.exposure})()";expectedJson='true'}
    )}
    $scenarioPath = Join-Path $session 'recipe-scenario.json'; $scenario | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $scenarioPath -Encoding utf8
    $probe=Start-Process -FilePath $probeExe -ArgumentList @('--scenario',$scenarioPath) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $session 'recipe.out') -RedirectStandardError (Join-Path $session 'recipe.err')
    if (-not $probe.WaitForExit(40000)) { Stop-Process -Id $probe.Id; throw 'Recipe DOM probe timeout' }
    $result = Get-Content -LiteralPath $scenario.outputPath -Raw | ConvertFrom-Json
    $deadline=[DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath ($manifestPath + '.recipe-edit.json')) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    Copy-Item -LiteralPath $scenario.outputPath -Destination (Join-Path $evidence 'v02-recipe-page-attempt.json')
    foreach ($suffix in @('run-a.json','run-b.json','recipe-edit.json')) {
        if (Test-Path -LiteralPath ($manifestPath+'.'+$suffix)) { Copy-Item -LiteralPath ($manifestPath+'.'+$suffix) -Destination (Join-Path $evidence ('v02-'+$suffix)) }
    }
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $evidence 'fixture-manifest.json')
    if($result.state -ne 'Passed'){throw ('Recipe page verification failed; evidence='+$evidence)}
    $edit=Get-Content -LiteralPath ($manifestPath+'.recipe-edit.json') -Raw|ConvertFrom-Json
    if($edit.state -ne 'Passed'){throw ('Formal A/B execution verification failed; evidence='+$evidence)}
    Write-Output ($result | ConvertTo-Json -Depth 8 -Compress)
    Write-Output ("Local isolated session: " + $session)
} finally {
    if (-not $fixture.HasExited) { Stop-Process -Id $fixture.Id }
    $env:GAODE_OFFLINE_OPERATOR_CREDENTIAL=$null;$env:GAODE_OFFLINE_ENGINEER_CREDENTIAL=$null;$env:GAODE_OFFLINE_VERIFY_RECIPE_EDIT_021=$null
}
