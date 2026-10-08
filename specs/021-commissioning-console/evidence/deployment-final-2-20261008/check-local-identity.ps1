$ErrorActionPreference='Stop'
$repo='D:\gaode'
$root='D:\Gaode-Station01\commissioning-021-final-2'
$framework=Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App' -Directory|Where-Object Name -like '10.*'|Sort-Object {[version]$_.Name} -Descending|Select-Object -First 1
foreach($name in @('Microsoft.Extensions.Primitives.dll','Microsoft.Extensions.Configuration.Abstractions.dll','Microsoft.Extensions.Configuration.dll')){
    [Reflection.Assembly]::LoadFrom((Join-Path $framework.FullName $name))|Out-Null
}
[Reflection.Assembly]::LoadFrom((Join-Path $root 'app/host/Gaode.Host.dll'))|Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $root 'app/host/Gaode.Application.dll'))|Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $root 'app/desktop/Gaode.Station01.Desktop.dll'))|Out-Null
$records=@((Get-Content -LiteralPath (Join-Path $root 'data/private-identity/host-identities.json') -Raw|ConvertFrom-Json).Gaode.CommissioningIdentities)
$values=[Collections.Generic.Dictionary[string,string]]::new()
for($i=0;$i -lt $records.Count;$i++){
    foreach($property in $records[$i].PSObject.Properties){$values['Gaode:CommissioningIdentities:'+$i+':'+$property.Name]=[string]$property.Value}
}
$builder=[Microsoft.Extensions.Configuration.ConfigurationBuilder]::new()
[Microsoft.Extensions.Configuration.MemoryConfigurationBuilderExtensions]::AddInMemoryCollection($builder,$values)|Out-Null
$registry=[Gaode.Host.Api.CommissioningIdentityRegistry]::new($builder.Build())
$checks=@()
foreach($role in @('operator','engineer')){
    & (Join-Path $repo 'scripts/Set-CommissioningIdentityEnvironment.ps1') -InstallationRoot $root -Role $role|Out-Null
    $hostConfiguration=[Gaode.Station01.Desktop.HostConfiguration]::FromEnvironment()
    $hostConfiguration.Validate()
    $profile=$hostConfiguration.CommissioningProfile
    $credential=[Environment]::GetEnvironmentVariable($profile.CredentialEnvironmentVariable)
    $identity=$registry.Find($credential)
    if($null -eq $identity -or $identity.SubjectId -ne $profile.ExpectedSubjectId -or $identity.Role -ne $profile.ExpectedRole){throw 'Formal identity/profile mismatch'}
    $checks+=@{role=$identity.Role;subjectId=$identity.SubjectId;desktopProfileValidated=$true;formalRegistryMatched=$true;permissions=[Gaode.Host.Api.CommissioningIdentityRegistry]::Permissions($identity.Role)}
}
$acl=Get-Acl -LiteralPath (Join-Path $root 'data/private-identity')
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
$access=@($acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]))
if(-not $acl.AreAccessRulesProtected -or $access.Count -ne 1 -or $access[0].IdentityReference -ne $sid){throw 'Local identity ACL mismatch'}
$saved=Get-Content -LiteralPath (Join-Path $root 'data/recipe-readback.json') -Raw|ConvertFrom-Json
$definition=$saved.recipes[0].definition
$template=Get-Content -LiteralPath (Join-Path $root 'data/config/console-template.json') -Raw|ConvertFrom-Json
$context=$template.contextTemplate
$context|Add-Member -NotePropertyName trayId -NotePropertyValue ([Guid]::NewGuid().ToString())
$context|Add-Member -NotePropertyName expectedRecipeRef -NotePropertyValue @{recipeId=$definition.recipeId;version=$definition.version;catalogDigest=$saved.catalogDigest}
$parsed=[Gaode.Application.Station01.StartRunContextParser]::Parse(($context|ConvertTo-Json -Depth 6))
if($parsed.Purpose.ToString() -ne 'Commissioning' -or $parsed.ScenarioId -ne $definition.scenarioId){throw 'Business template context mismatch'}
$report=@{schemaVersion='commissioning-local-identity-check/1';passed=$true;formalTypesLoadedWithoutApplicationStart=$true;roles=$checks;aclCurrentUserOnly=$true;credentialValuesOmitted=$true;hardwareConnected=$false;motionAuthorized=$false;businessContextFormalParserPassed=$true;requestDispatched=$false}
$report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'local-identity-check.json') -Encoding utf8
$report|ConvertTo-Json -Depth 6
