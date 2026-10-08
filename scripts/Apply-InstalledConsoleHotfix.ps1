[CmdletBinding()]
param([Parameter(Mandatory)][string]$InstallationRoot,
      [Parameter(Mandatory)][string]$PatchPath,[switch]$CheckOnly)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($InstallationRoot).TrimEnd('\','/')
$patch=[IO.Path]::GetFullPath($PatchPath)
$manifest=Get-Content -LiteralPath (Join-Path $patch 'hotfix-manifest.json') -Raw|ConvertFrom-Json
if($manifest.schemaVersion -ne 'commissioning-installed-hotfix/1'){throw 'Unknown hotfix schema'}
function Child($base,$relative){
    if([IO.Path]::IsPathRooted($relative)){throw 'Absolute hotfix path rejected'}
    $resolved=[IO.Path]::GetFullPath((Join-Path $base $relative))
    if(-not $resolved.StartsWith($base.TrimEnd('\','/')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path escapes hotfix root'}
    return $resolved
}
$files=@($manifest.files)
foreach($file in $files){
    $inputPath=Child (Join-Path $patch 'payload') $file.path
    $targetPath=Child $root $file.path
    if((Get-FileHash -LiteralPath $inputPath).Hash -ne $file.sha256){throw ('Hotfix payload mismatch: '+$file.path)}
    if((Get-FileHash -LiteralPath $targetPath).Hash -ne $file.baseSha256){throw ('Installed baseline changed: '+$file.path)}
}
if($CheckOnly){Write-Output ('Hotfix hashes verified: '+$files.Count+' files; no files applied, no device connection.');return}
$active=@(Get-CimInstance Win32_Process|Where-Object {
    $_.ExecutablePath -and $_.ExecutablePath.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)
})
if($active.Count){throw '请先使用一键关闭退出本安装的桌面、后台和相机进程，再应用更新。未关闭进程、未覆盖文件。'}
$backup=Child $root ('data/hotfix-backups/'+$manifest.id+'-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($backup)|Out-Null
# Back up every baseline before replacing any file. Never touch run databases or identities.
foreach($file in $files){
    $saved=Child $backup $file.path
    [IO.Directory]::CreateDirectory((Split-Path $saved))|Out-Null
    Copy-Item -LiteralPath (Child $root $file.path) -Destination $saved
}
try{
    foreach($file in $files){
        Copy-Item -LiteralPath (Child (Join-Path $patch 'payload') $file.path) -Destination (Child $root $file.path) -Force
        if((Get-FileHash -LiteralPath (Child $root $file.path)).Hash -ne $file.sha256){throw ('Applied file mismatch: '+$file.path)}
    }
}catch{
    foreach($file in $files){Copy-Item -LiteralPath (Child $backup $file.path) -Destination (Child $root $file.path) -Force}
    throw
}
@{utc=[DateTimeOffset]::UtcNow;id=$manifest.id;files=$files.Count;backup=$backup;
    devicesConnected=$false;automaticallyStarted=$false}|ConvertTo-Json -Compress|
    Add-Content -LiteralPath (Join-Path $root 'data/hotfix-history.jsonl') -Encoding utf8
Write-Output ('更新已应用并校验；原文件备份：'+$backup+'。未启动程序，未复位PLC，未解除旧任务。')
