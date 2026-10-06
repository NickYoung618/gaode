$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$stage = Join-Path $repo 'artifacts/q01-q02-local-package/gaode-q01-q02-local-test'
$zip = Join-Path $repo 'artifacts/gaode-q01-q02-local-test-win-x64.zip'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
function Copy-Relative([string]$relative) {
    $source = Join-Path $repo $relative
    $target = Join-Path $stage $relative
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}
foreach ($file in @('Start.ps1', 'Stop.ps1', 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $stage $file)
}
Push-Location $repo
try {
    $sourceFiles = @(rg --files backend/src backend/tools/Gaode.StorePrep VirtualPlc desktop frontend `
        -g '!**/bin/**' -g '!**/obj/**' -g '!**/node_modules/**' -g '!**/dist/**' -g '!**/tests/**')
    foreach ($file in $sourceFiles) { Copy-Relative $file }
    foreach ($file in @('global.json',
        'backend/Directory.Packages.props',
        'backend/Directory.Build.props',
        'scripts/start-station01-virtual-loop.ps1',
        'scripts/simulate-station01-load.ps1',
        'scripts/virtual-station01-algorithm.py',
        'specs/001-station01-public-preparation/contracts/public-config.schema.json',
        'specs/001-station01-public-preparation/contracts/budget.schema.json',
        'specs/001-station01-public-preparation/contracts/simulation.schema.json',
        'specs/001-station01-public-preparation/contracts/test-media-fixture.schema.json')) {
        Copy-Relative $file
    }
    foreach ($file in @(rg --files specs/007-station01-integrated-loop/examples `
        -g '*.json')) { Copy-Relative $file }
    foreach ($file in @(rg --files specs/007-station01-integrated-loop/fixtures/images `
        -g '*.png')) { Copy-Relative $file }
    foreach ($name in @('fixture.json', 'fixture-q02.json', 'recipes.json', 'recipes-q02.json',
        'media-manifest.json', 'media-manifest-q02.json', 'worker-manifest.json', 'worker-manifest-q02.json')) {
        Copy-Relative "specs/008-recipe-driven-inspection/fixtures/$name"
    }
    foreach ($file in @(rg --files third-party-licenses)) { Copy-Relative $file }
    Copy-Item -LiteralPath (Join-Path $repo 'frontend/dist') `
        -Destination (Join-Path $stage 'frontend/dist') -Recurse
    $launch = Join-Path $stage 'scripts/start-station01-virtual-loop.ps1'
    $source = Get-Content -LiteralPath $launch -Raw
    $old = 'dotnet run --project (Join-Path $repo ''backend/tools/Gaode.StorePrep/Gaode.StorePrep.csproj'') -- $allowed $TestRoot'
    $new = '& dotnet (Join-Path $repo ''backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll'') $allowed $TestRoot'
    if (-not $source.Contains($old)) { throw 'StorePrep 启动行已变化，不能自动打包。' }
    $source.Replace($old, $new) | Set-Content -LiteralPath $launch -Encoding utf8

    Push-Location $stage
    try {
    $publishes = @(
        @{ project = 'backend/src/Gaode.Host/Gaode.Host.csproj'; output = 'backend/src/Gaode.Host/bin/Debug/net10.0' },
        @{ project = 'VirtualPlc/VirtualPlc.csproj'; output = 'VirtualPlc/bin/Debug/net10.0' },
        @{ project = 'backend/tools/Gaode.StorePrep/Gaode.StorePrep.csproj'; output = 'backend/tools/Gaode.StorePrep/bin/Debug/net10.0' },
        @{ project = 'desktop/Gaode.Station01.Desktop.csproj'; output = 'desktop/bin/Release/net10.0-windows10.0.17763.0' }
    )
    foreach ($item in $publishes) {
        $configuration = if ($item.project -like 'desktop/*') { 'Release' } else { 'Debug' }
        $output = Join-Path $stage $item.output
        & dotnet publish $item.project -c $configuration -o $output --nologo
        if ($LASTEXITCODE -ne 0) { throw "发布失败：$($item.project)" }
    }
    } finally { Pop-Location }
    $required = @(
        'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll',
        'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll',
        'backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll',
        'desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe',
        'desktop/bin/Release/net10.0-windows10.0.17763.0/frontend/dist/runtime.js')
    foreach ($file in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $stage $file))) { throw "发布内容缺失：$file" }
    }
    # Published desktop already carries the approved built pages and their assets.
    # Keep one runtime copy of those large assets in the archive.
    foreach ($relative in @('frontend/dist', 'frontend/src/assets',
        'backend/src/Gaode.Domain/bin', 'backend/src/Gaode.Application/bin',
        'backend/src/Gaode.Infrastructure/bin')) {
        $target = [IO.Path]::GetFullPath((Join-Path $stage $relative))
        if (-not $target.StartsWith($stage + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)) { throw '打包清理路径越界。' }
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    }
    $objDirs = @(Get-ChildItem -LiteralPath $stage -Recurse -Directory -Filter obj)
    foreach ($directory in $objDirs) {
        $target = [IO.Path]::GetFullPath($directory.FullName)
        if (-not $target.StartsWith($stage + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)) { throw '打包清理路径越界。' }
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    Write-Host "压缩包：$zip"
    Write-Host "SHA256：$hash"
    Write-Host "大小：$([math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)) MB"
} finally { Pop-Location }
