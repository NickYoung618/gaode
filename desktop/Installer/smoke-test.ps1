[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
$dist = Join-Path $PackageDirectory 'frontend\dist'
foreach($page in @('login.html','a.html','data-view.html')) { if(-not (Test-Path (Join-Path $dist $page))){ throw "Missing page $page" } }
if(-not (Test-Path (Join-Path $dist 'prototype.html'))){ throw 'Missing approved navigation alias prototype.html' }
if(-not (Test-Path (Join-Path $dist 'runtime.js'))){ throw 'Missing local runtime.js' }
$external = Get-ChildItem $dist -Recurse -File | Select-String -Pattern 'https://cdn\.tailwindcss\.com|https://unpkg\.com/lucide|fonts\.googleapis\.com'
if($external){ throw 'External CDN reference found in packaged resources' }
Write-Output 'Package static-resource smoke test passed.'
