#requires -Version 7.4
$ErrorActionPreference='Stop'
$cases=Get-Content (Join-Path $PSScriptRoot 'cases.json') -Raw | ConvertFrom-Json -AsHashtable
$names=@($cases.Keys | Sort-Object)
for($i=0;$i -lt $names.Count;$i++){Write-Host ("{0,2}. {1}" -f ($i+1),$names[$i])}
Write-Host 'Q07/Q10/Q12/Q13/Q16/Q17/Q19/Q22已退出当前范围，见测试配方清单.md。'
$answer=Read-Host '输入配方名称（例如Q04、Q01-NG）或菜单序号；直接回车为Q01'
$choice=if([string]::IsNullOrWhiteSpace($answer)){'Q01'}else{$answer.Trim()}
$number=0
if([int]::TryParse($choice,[ref]$number)){if($number -lt 1 -or $number -gt $names.Count){throw '序号不在菜单范围。'};$choice=$names[$number-1]}
if(-not $cases.ContainsKey($choice)){throw '请输入菜单中的配方名称。'}
& (Join-Path $PSScriptRoot 'Start.ps1') -Case $choice
