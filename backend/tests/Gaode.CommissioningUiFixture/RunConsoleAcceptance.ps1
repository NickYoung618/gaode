$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$evidence=Join-Path $repo ('specs/021-commissioning-console/evidence/console-'+$stamp)
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$session=Join-Path ([IO.Path]::GetTempPath()) ('gaode-021-console-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($session)|Out-Null
$op=[Guid]::NewGuid().ToString('N');$eng=[Guid]::NewGuid().ToString('N')
$env:GAODE_OFFLINE_OPERATOR_CREDENTIAL=$op;$env:GAODE_OFFLINE_ENGINEER_CREDENTIAL=$eng
$env:GAODE_OFFLINE_DROP_START_RESPONSE='1'
$manifestPath=Join-Path $session 'manifest.json'
$fixture=Start-Process -FilePath (Join-Path $PSScriptRoot 'bin/Debug/net10.0/Gaode.CommissioningUiFixture.exe') -ArgumentList @('--mode','commissioning','--output',$manifestPath) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $session 'fixture.out') -RedirectStandardError (Join-Path $session 'fixture.err')
function Step($id,$script,$wait=$false) { if($wait){@{id=$id;script=$script;waitUntil=$true}}else{@{id=$id;script=$script;expectedJson='true'}} }
function RunCase($name,$profile,$steps) {
    if($env:GAODE_OFFLINE_CASE_FILTER -and $name -ne $env:GAODE_OFFLINE_CASE_FILTER){return}
    $env:GAODE_COMMISSIONING_PROFILE_PATH=if($profile -eq 'engineer'){$manifest.engineerProfile}else{$manifest.operatorProfile}
    $env:GAODE_OFFLINE_OPERATOR_CREDENTIAL=if($profile -eq 'operator'){$op}else{$null}
    $env:GAODE_OFFLINE_ENGINEER_CREDENTIAL=if($profile -eq 'engineer'){$eng}else{$null}
    $scenario=@{scope='OFFLINE';timeoutSeconds=55;outputPath=(Join-Path $evidence ($name+'.json'));steps=$steps}
    $path=Join-Path $session ($name+'.scenario.json');$scenario|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $path -Encoding utf8
    $probe=Start-Process -FilePath (Join-Path $repo 'desktop/tests/Gaode.Desktop.CommissioningProbe/bin/Debug/net10.0-windows10.0.17763.0/Gaode.Desktop.CommissioningProbe.exe') -ArgumentList @('--scenario',$path) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $session ($name+'.out')) -RedirectStandardError (Join-Path $session ($name+'.err'))
    if(-not $probe.WaitForExit(60000)){Stop-Process -Id $probe.Id;throw ($name+': probe timeout')}
    $result=Get-Content -LiteralPath $scenario.outputPath -Raw|ConvertFrom-Json
    Write-Output ($name+': '+$result.state)
    if($result.state -ne 'Passed'){throw ($name+': inspect '+$scenario.outputPath)}
}
try {
    $due=[DateTime]::UtcNow.AddSeconds(40)
    while(-not(Test-Path -LiteralPath $manifestPath)){if($fixture.HasExited -or [DateTime]::UtcNow -gt $due){throw ('Fixture startup failed: '+$session)};Start-Sleep -Milliseconds 100}
    $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
    $env:GAODE_MODE='RealDeviceCommissioning';$env:GAODE_API_BASE_URL=$manifest.apiBaseUrl;$env:GAODE_SIGNALR_URL=$manifest.signalRUrl;$env:GAODE_FRONTEND_DIST=Join-Path $repo 'frontend/dist'
    $engineer=@(
      (Step 'identity' "document.getElementById('user')?.value==='OFFLINE engineer'" $true),
      (Step 'login' "window.handleLogin({preventDefault(){}})"),
      (Step 'page' "document.readyState==='complete' && !!window.station01 && location.pathname==='/prototype.html'" $true),
      (Step 'open' "document.getElementById('btnRecipe').click();true"),
      (Step 'catalog' "document.getElementById('recipeAuthoringCatalog')?.options.length>1" $true),
      (Step 'load' "(()=>{const s=document.getElementById('recipeAuthoringCatalog');s.value=s.options[1].value;s.dispatchEvent(new Event('change',{bubbles:true}));return true})()"),
      (Step 'loaded' "document.getElementById('recipeAuthoringNotice').textContent.includes('已读取保存内容')" $true),
      (Step 'virtual-light' "(()=>{const e=document.querySelector('[data-light-mode]');e.checked=true;e.dispatchEvent(new Event('change',{bubbles:true}));return true})()"),
      (Step 'F-X' "(()=>{const e=document.querySelector('input[aria-label=`"虚拟算法 F读码X (mm)`"]');e.value='10';e.dispatchEvent(new Event('change',{bubbles:true}));return true})()"),
      (Step 'F-Y' "(()=>{const e=document.querySelector('input[aria-label=`"虚拟算法 F读码Y (mm)`"]');e.value='20';e.dispatchEvent(new Event('change',{bubbles:true}));return true})()"),
      (Step 'save' "document.getElementById('recipeAuthoringSave').click();true"),
      (Step 'reread' "document.getElementById('recipeAuthoringNotice').textContent.includes('已保存并重读')" $true),
      (Step 'virtual-persisted' "document.querySelector('[data-authoring-section=basic]').click();document.querySelector('[data-light-mode]').checked===true && Number(document.querySelector('input[aria-label=`"虚拟算法 F读码X (mm)`"]')?.value)===10"),
      (Step 'points' "document.querySelector('[data-authoring-section=points]').click();Array.from(document.querySelectorAll('.recipe-nav-button')).find(b=>b.textContent.includes('第1检测面')).click();true"),
      (Step 'brightness-skipped-camera-required' "document.querySelector('.recipe-capture-fields input[aria-label=`"光源亮度 (%)`"]')?.disabled===true && document.querySelector('.recipe-capture-fields input[aria-label=`"曝光 (µs)`"]')?.disabled===false")
    )
    RunCase 'v08-page-light-F' 'engineer' $engineer
    $operator=@(
      (Step 'identity' "document.getElementById('user')?.value==='OFFLINE operator'" $true),
      (Step 'login' "window.handleLogin({preventDefault(){}})"),
      (Step 'page' "document.readyState==='complete' && !!window.station01 && location.pathname==='/prototype.html'" $true),
      (Step 'open-readonly' "document.getElementById('btnRecipe').click();true"),
      @{id='recipe-page-state';script="JSON.stringify({disabled:document.getElementById('btnRecipe').disabled,authoring:!!window.GaodeRecipeAuthoring,notice:document.getElementById('recipeAuthoringNotice').textContent})"},
      (Step 'catalog' "document.getElementById('recipeAuthoringCatalog')?.options.length>1" $true),
      (Step 'no-create' "document.getElementById('recipeAuthoringNew').disabled===true"),
      (Step 'select' "(()=>{const s=document.getElementById('recipeAuthoringCatalog');s.value=s.options[1].value;s.dispatchEvent(new Event('change',{bubbles:true}));return true})()"),
      (Step 'loaded' "document.getElementById('recipeAuthoringNotice').textContent.includes('已读取保存内容')" $true),
      (Step 'no-write' "document.getElementById('recipeAuthoringSave').disabled===true && document.querySelector('[data-light-mode]').disabled===true"),
      (Step 'close-and-start' "closeRecipe();window.__OFFLINE_RUN__=null;window.addEventListener('station01:status',e=>{if(e.detail.run)window.__OFFLINE_RUN__=e.detail.run});Array.from(document.querySelectorAll('button')).find(b=>b.textContent.includes('启动')).click();true"),
      (Step 'lost-receipt-recovered' "!!window.__OFFLINE_RUN__?.runId" $true),
      (Step 'reload-only-query' "window.location.reload();true"),
      (Step 'same-run-after-reload' "!!window.station01 && Array.from({length:localStorage.length},(_,i)=>localStorage.key(i)).some(k=>k.startsWith('gaode:station01:pending:'))" $true)
    )
    RunCase 'v03-page-query-operator' 'operator' $operator
    $run1=$manifest.mediaRuns[0];$run2=$manifest.mediaRuns[1]
    $media=@(
      (Step 'identity' "document.getElementById('user')?.value==='OFFLINE operator'" $true),
      (Step 'navigate-known-media' "location.href='prototype.html?runId=$run1';true"),
      (Step 'seven-current-images' "document.querySelectorAll('#camGrid img[src][data-media-id]').length===7" $true),
      @{id='observed-mapping';script="JSON.stringify(Array.from(document.querySelectorAll('#camGrid img')).map(i=>i.dataset.businessCamera))"},
      (Step 'confirmed-mapping' "JSON.stringify(Array.from(document.querySelectorAll('#camGrid img')).map(i=>i.dataset.businessCamera))===JSON.stringify(['C','D','A','B','E','ThreeD','F'])"),
      (Step 'capture-before-pin' "window.__OFFLINE_FIRST_MEDIA__=document.querySelector('#camGrid img').dataset.mediaId;document.querySelector('#camGrid > div').click();true"),
      (Step 'same-camera-other-frame' "document.querySelector('#camGrid img').dataset.mediaId!==window.__OFFLINE_FIRST_MEDIA__" $true),
      (Step 'next-run' "location.href='prototype.html?runId=$run2';true"),
      (Step 'absent-not-old' "document.querySelectorAll('#camGrid img[src][data-media-id]').length===1 && document.querySelector('#camGrid img[src]').dataset.runId.toLowerCase()==='$run2'.toLowerCase()" $true),
      (Step 'known-data-page' "location.href='data-view.html?runId=$run2';true"),
      (Step 'no-demo-product' "!!window.station01 && document.getElementById('partsTbody').textContent.includes('暂无已提交检测结果') && !document.getElementById('partsTbody').textContent.includes('大底座')" $true),
      (Step 'no-demo-trend' "document.getElementById('trendChart').children.length===0")
    )
    # Separate probe's WebView storage root includes profile; clear only our fixture pending reference before known-run media navigation.
    $media=@($media[0],(Step 'clear-offline-pending' "Object.keys(localStorage).filter(k=>k.startsWith('gaode:station01:pending:')).forEach(k=>localStorage.removeItem(k));true"))+@($media[1..($media.Count-1)])
    RunCase 'v06-page-media-data' 'operator' $media
    Stop-Process -Id $fixture.Id
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $evidence 'fixture-manifest.json')
    Copy-Item -LiteralPath (Join-Path $manifest.root 'host.jsonl') -Destination (Join-Path $evidence 'host.jsonl')
    $headers=@{Authorization='Bearer '+$eng}
    Write-Output ('Evidence: '+$evidence)
} finally {
    if(-not $fixture.HasExited){Stop-Process -Id $fixture.Id}
    $env:GAODE_OFFLINE_OPERATOR_CREDENTIAL=$null;$env:GAODE_OFFLINE_ENGINEER_CREDENTIAL=$null;$env:GAODE_OFFLINE_DROP_START_RESPONSE=$null
}
