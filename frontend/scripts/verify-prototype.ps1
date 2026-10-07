[CmdletBinding()]
param(
    [string]$Archive = 'E:\dzk\gaode\原型.zip',
    [string]$Output = '',
    [string]$FrontendRoot = ''
)
$ErrorActionPreference = 'Stop'
if (-not $FrontendRoot) { $FrontendRoot = Split-Path -Parent $PSScriptRoot }
if (-not $Output) {
    $evidence = if ($env:GAODE_FRONTEND_EVIDENCE_ROOT) { $env:GAODE_FRONTEND_EVIDENCE_ROOT } else {
        Join-Path $FrontendRoot '../artifacts/recipe-authoring-012/prototype'
    }
    $Output = Join-Path $evidence ('prototype-' + [Guid]::NewGuid().ToString('N') + '.json')
}
$manifest = Get-Content -LiteralPath (Join-Path $FrontendRoot 'scripts/recipe-authoring-012-differences.json') -Raw -Encoding utf8 | ConvertFrom-Json
$archiveHash = (Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash
$baseline = '3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0'
if ($archiveHash -ne $baseline -or $manifest.archiveSha256 -ne $baseline) { throw 'Prototype archive hash mismatch' }
function Get-TextHash([string]$Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))) -replace '-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Archive))
$results = @()
try {
    foreach ($page in @('a.html', 'data-view.html', 'login.html')) {
        $entries = @($zip.Entries | Where-Object { $_.FullName -eq $page -or $_.FullName.EndsWith('/' + $page) })
        if ($entries.Count -ne 1) { throw "Missing or ambiguous archive page: $page" }
        $reader = New-Object IO.StreamReader($entries[0].Open(), [Text.Encoding]::UTF8, $true)
        try { $approved = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $entry = @($manifest.pages | Where-Object page -eq $page)
        if ($entry.Count -ne 1 -or (Get-TextHash $approved) -ne $entry[0].archiveTextSha256) { throw "Archive page mismatch: $page" }
        $position = 0
        $expected = New-Object Text.StringBuilder
        foreach ($patch in $entry[0].patches) {
            if (-not $patch.requirement -or -not $patch.region -or $patch.offset -lt $position -or
                $patch.offset + $patch.before.Length -gt $approved.Length -or
                $approved.Substring($patch.offset, $patch.before.Length) -cne $patch.before -or
                (Get-TextHash $patch.before) -ne $patch.beforeSha256 -or
                (Get-TextHash $patch.after) -ne $patch.afterSha256) { throw "Invalid exact authorized replacement: $page/$($patch.id)" }
            [void]$expected.Append($approved.Substring($position, $patch.offset - $position))
            [void]$expected.Append($patch.after)
            $position = $patch.offset + $patch.before.Length
        }
        [void]$expected.Append($approved.Substring($position))
        $source = [IO.File]::ReadAllText((Join-Path $FrontendRoot "src/pages/$page"))
        if ($expected.ToString() -cne $source) { throw "Unlisted source difference: $page" }
        # Finite offline-resource substitutions from build.mjs; no script/page exclusion.
        $built = $source.Replace('<script src="https://cdn.tailwindcss.com"></script>', '<link rel="stylesheet" href="./vendor/tailwind.css" />')
        $built = $built.Replace('https://unpkg.com/lucide@latest', './vendor/lucide.min.js')
        $built = [regex]::Replace($built, '<link rel="preconnect" href="https://fonts\.[^"]*"[^>]*/>\r?\n?', '')
        $built = [regex]::Replace($built, '<link[^>]+href="https://fonts\.googleapis\.com[^"]*"[^>]*/>', '<link rel="stylesheet" href="./vendor/fonts.css" />')
        $authoring = if ($page -eq 'a.html') { '  <script src="./recipe-authoring.js"></script>' + [char]10 + '  <script src="./public-tray-flow.js"></script>' + [char]10 } else { '' }
        $injection = '  <script src="./vendor/signalr.min.js"></script>' + [char]10 + $authoring + '  <script src="./runtime.js"></script>' + [char]10 + '</body>'
        $built = $built.Replace('</body>', $injection)
        if ([IO.File]::ReadAllText((Join-Path $FrontendRoot "dist/$page")) -cne $built) { throw "Unlisted built-page difference: $page" }
        if ($page -eq 'a.html' -and [IO.File]::ReadAllText((Join-Path $FrontendRoot 'dist/prototype.html')) -cne $built) {
            throw 'Unlisted prototype navigation alias difference'
        }
        $results += [ordered]@{ page=$page; archiveTextSha256=(Get-TextHash $approved); sourceSha256=(Get-TextHash $source); builtSha256=(Get-TextHash $built); exactReplacements=@($entry[0].patches).Count }
    }
    foreach ($resource in $manifest.resources) {
        $path = Join-Path $FrontendRoot $resource.path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path).Hash -ne $resource.sha256) {
            throw "Unlisted resource difference: $($resource.path)"
        }
    }
    $parent = Split-Path -Parent ([IO.Path]::GetFullPath($Output))
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    if (Test-Path -LiteralPath $Output) { throw 'Do not overwrite an existing prototype evidence report' }
    [ordered]@{ archiveSha256=$archiveHash; checkedAtUtc=[DateTime]::UtcNow.ToString('o'); status='Passed';
        manifestSha256=(Get-FileHash -LiteralPath (Join-Path $FrontendRoot 'scripts/recipe-authoring-012-differences.json')).Hash;
        pages=$results; resources=@($manifest.resources).Count } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Output -Encoding utf8
    Write-Output "Prototype exact replacements and delivered resources verified: $Output"
} finally { $zip.Dispose() }
