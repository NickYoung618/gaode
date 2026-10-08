param([Parameter(Mandatory)][string]$InstallationRoot,[ValidateSet('operator','engineer')][string]$Role='operator')
$ErrorActionPreference='Stop'
if(-not [IO.Path]::IsPathFullyQualified($InstallationRoot)){throw 'Absolute installation path required'}
$root=[IO.Path]::GetFullPath($InstallationRoot)
$settings=Get-Content -LiteralPath (Join-Path $root 'data/private-identity/host-identities.json') -Raw|ConvertFrom-Json
$profilePath=Join-Path $root ('data/config/desktop-'+$Role+'.json')
$profile=Get-Content -LiteralPath $profilePath -Raw|ConvertFrom-Json
$records=@($settings.Gaode.CommissioningIdentities)
for($i=0;$i -lt $records.Count;$i++){
    foreach($property in $records[$i].PSObject.Properties){[Environment]::SetEnvironmentVariable(('Gaode__CommissioningIdentities__'+$i+'__'+$property.Name),[string]$property.Value,'Process')}
}
$selected=@($records|Where-Object ProfileId -eq $profile.profileId)
if($selected.Count -ne 1){throw 'Selected controlled identity not unique'}
foreach($other in @('operator','engineer')){[Environment]::SetEnvironmentVariable(('GAODE_COMMISSIONING_'+$other.ToUpperInvariant()+'_CREDENTIAL'),$null,'Process')}
[Environment]::SetEnvironmentVariable($profile.credentialEnvironmentVariable,$selected[0].Credential,'Process')
$env:GAODE_COMMISSIONING_PROFILE_PATH=$profilePath
$env:GAODE_MODE='RealDeviceCommissioning'
Write-Output ('Local identity environment selected: '+$profile.expectedRole+'; no application or device started.')
