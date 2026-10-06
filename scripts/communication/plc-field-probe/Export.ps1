#requires -Version 5.1
[CmdletBinding()]
param([string]$SessionDirectory = '')
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
function File-Sha256([string]$Path) {
    $hash=[Security.Cryptography.SHA256]::Create(); $inputFile=[IO.File]::OpenRead($Path)
    try { return [BitConverter]::ToString($hash.ComputeHash($inputFile)).Replace('-','') }
    finally { $inputFile.Dispose(); $hash.Dispose() }
}
if (-not $SessionDirectory) { $SessionDirectory = Join-Path $PSScriptRoot 'runs' }
if (-not (Test-Path -LiteralPath $SessionDirectory -PathType Container)) { throw '没有测试记录可导出。' }
$items = @((Resolve-Path -LiteralPath $SessionDirectory).Path)
foreach ($name in @('site.json','field-notes.json','package-manifest.json','Probe.ps1','Export.ps1')) {
    $path = Join-Path $PSScriptRoot $name
    if (Test-Path -LiteralPath $path) { $items += $path }
}
$manifest = [ordered]@{
    schemaVersion=1
    exportedUtc=[DateTimeOffset]::UtcNow.ToString('o')
    toolVersion='0.2.0'
    sessions=@(Get-ChildItem -LiteralPath $SessionDirectory -Filter config.snapshot.json -Recurse | ForEach-Object { $_.DirectoryName })
    files=@($items | ForEach-Object { Get-ChildItem -LiteralPath $_ -File -Recurse } | ForEach-Object {
        [ordered]@{name=$_.FullName;bytes=$_.Length;sha256=(File-Sha256 $_.FullName)}
    })
    note='现场原始回传，包含失败记录；有summary不代表真机或整机通过。未自动上传。'
}
$utf8=New-Object System.Text.UTF8Encoding($false)
$manifestPath=Join-Path $PSScriptRoot 'return-manifest.json'
[IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 8),$utf8)
$items += $manifestPath
$destination=Join-Path $PSScriptRoot ('PLC-return-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6)+'.zip')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::Open($destination,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($item in $items) {
        $entry=Get-Item -LiteralPath $item
        if ($entry.PSIsContainer) {
            foreach ($file in (Get-ChildItem -LiteralPath $entry.FullName -Recurse -File)) {
                $relative=$entry.Name+'/'+$file.FullName.Substring($entry.FullName.Length+1).Replace('\','/')
                [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative)
            }
        } else { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$entry.FullName,$entry.Name) }
    }
} finally { $zip.Dispose() }
Write-Host "回传文件：$destination"
Write-Host "SHA256：$(File-Sha256 $destination)"
