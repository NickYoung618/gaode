#requires -Version 7.4
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($PSScriptRoot)
$base=Join-Path $root 'artifacts/recipe-execution-008'
$stamp=[datetime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$destination=Join-Path $root "diagnostics/$stamp"
New-Item -ItemType Directory -Path $destination | Out-Null
$errorsFound=@(); $hashes=@(); $runs=@()
function Copy-LocalEvidence([string]$file,[string]$relative) {
    if(-not (Test-Path -LiteralPath $file -PathType Leaf)){return}
    if(-not [IO.Path]::GetFullPath($file).StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence outside this package'}
    $target=Join-Path $destination $relative
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $file -Destination $target
}
foreach($relative in @('package-manifest.json','Start.ps1','VirtualPlc/wwwroot/index.html','VirtualPlc/wwwroot/app.js',
 'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll','backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll',
 'scripts/virtual-station01-algorithm.py','frontend/dist/runtime.js')) {
    $file=Join-Path $root $relative
    if(Test-Path -LiteralPath $file){$hashes+=@{path=$relative;sha256=(Get-FileHash -LiteralPath $file).Hash;lastWriteUtc=(Get-Item -LiteralPath $file).LastWriteTimeUtc}}
}
Copy-LocalEvidence (Join-Path $root 'package-manifest.json') 'package-manifest.json'
if(Test-Path $base) {
    Copy-LocalEvidence (Join-Path $base 'local-active.json') 'local-active.json'
    foreach($run in @(Get-ChildItem -LiteralPath $base -Directory -Filter 'local-*' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 3)) {
        $runs+=$run.Name
        foreach($relative in @('process.json','media-root/worker-protocol.jsonl')) {
            Copy-LocalEvidence (Join-Path $run.FullName $relative) (Join-Path $run.Name $relative)
        }
        foreach($folder in @($run.FullName,(Join-Path $run.FullName 'logs'))) {
            if(Test-Path $folder) {foreach($file in @(Get-ChildItem -LiteralPath $folder -File | Where-Object {$_.Extension -in @('.log','.jsonl')})) {
                Copy-LocalEvidence $file.FullName (Join-Path $run.Name ([IO.Path]::GetRelativePath($run.FullName,$file.FullName)))
            }}
        }
    }
    $marker=Join-Path $base 'local-active.json'
    if(Test-Path $marker) {
        $active=Get-Content $marker -Raw | ConvertFrom-Json
        $processFile=[IO.Path]::GetFullPath($active.processFile)
        if($processFile.StartsWith($base+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -and (Test-Path $processFile)) {
            $record=Get-Content $processFile -Raw | ConvertFrom-Json
            $uri=[uri]$record.plcApiBase
            if($uri.IsLoopback -and $uri.Scheme -eq 'http') {
                foreach($entry in @(@('/api/simulator/state','state.json'),@('/api/simulator/audit','audit.json'),@('/api/simulator/changes?after=0','changes.json'),@('/','served-index.html'),@('/app.js','served-app.js'))) {
                    try {Invoke-WebRequest ($uri.GetLeftPart('Authority')+$entry[0]) -TimeoutSec 3 -OutFile (Join-Path $destination $entry[1])}
                    catch {$errorsFound+=@{resource=$entry[0];error=$_.Exception.Message}}
                }
            }
        }
    }
}
@{utc=$stamp;packageRoot=$root;runs=$runs;fileHashes=$hashes;readErrors=$errorsFound;readOnly=$true;deviceCommandsSent=0} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $destination 'collection.json') -Encoding utf8
$zip=$destination+'.zip'
Compress-Archive -LiteralPath $destination -DestinationPath $zip
Write-Host "诊断包已生成：$zip"
Write-Host '本操作只读取本包日志和本地PLC监控，不执行运动、复位或停止。请将诊断包提供给开发人员。'
