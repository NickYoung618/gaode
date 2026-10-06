[CmdletBinding()]
param([ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$frontend = Join-Path $root 'frontend'
$project = Join-Path $root 'desktop\Gaode.Station01.Desktop.csproj'
$out = Join-Path $root "artifacts\frontend\package\Gaode-Station01-$Runtime"
New-Item -ItemType Directory -Force -Path $out | Out-Null
Push-Location $frontend
try { npm run build } finally { Pop-Location }
dotnet publish $project --configuration Release --runtime $Runtime --self-contained false --output $out
$dist = Join-Path $out 'frontend\dist'
foreach($page in @('login.html','a.html','data-view.html')) { if(-not (Test-Path (Join-Path $dist $page))){ throw "Missing packaged page: $page" } }
$hash = (Get-Content (Join-Path $dist 'prototype-hash.txt') -Raw).Trim()
if($hash -ne '3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0'){ throw 'Packaged prototype hash mismatch' }
$manifest = [ordered]@{ runtime=$Runtime; mode='Test/Simulation/Production configured by host'; prototypeSha256=$hash; pages=@('login.html','a.html','data-view.html'); createdAtUtc=[DateTime]::UtcNow.ToString('o') }
$manifest | ConvertTo-Json | Set-Content (Join-Path $out 'package-manifest.json') -Encoding utf8
$zip = "$out.zip"; if(Test-Path $zip){ Remove-Item -LiteralPath $zip -Force }; Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -Force
Write-Output "Package: $zip"
