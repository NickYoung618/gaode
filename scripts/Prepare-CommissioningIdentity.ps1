param([Parameter(Mandatory)][string]$InstallationRoot)
$ErrorActionPreference='Stop'
if(-not [IO.Path]::IsPathFullyQualified($InstallationRoot)){throw 'Absolute installation path required'}
$root=[IO.Path]::GetFullPath($InstallationRoot)
$definition=(Get-Content -LiteralPath (Join-Path $root 'data/recipe-readback.json') -Raw|ConvertFrom-Json).recipes[0].definition
$inputs=Get-Content -LiteralPath (Join-Path $root 'data/commissioning-inputs.json') -Raw|ConvertFrom-Json
if($definition.recipeId -ne $inputs.recipeId -or $definition.version -ne $inputs.version){throw 'Saved recipe/input reference mismatch'}
$private=Join-Path $root 'data/private-identity'
if(Test-Path -LiteralPath $private){throw 'Identity already prepared; existing credentials retained'}
[IO.Directory]::CreateDirectory($private)|Out-Null
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
$acl=[Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true,$false)
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
Set-Acl -LiteralPath $private -AclObject $acl
$config=Join-Path $root 'data/config'
$template=Join-Path $config 'console-template.json'
$profiles=@();$identities=@()
foreach($entry in @(@('operator','Operator','联调操作员'),@('engineer','ProcessEngineer','联调工艺工程师'))){
    $name=$entry[0];$profileId='station01-commissioning-'+$name
    $identities+=@{ProfileId=$profileId;SubjectId=('commissioning:station01:'+$name);DisplayName=$entry[2];Role=$entry[1];Purpose='RealDeviceCommissioning';Credential=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))}
    $path=Join-Path $config ('desktop-'+$name+'.json')
    if(Test-Path -LiteralPath $path){throw 'Desktop profile exists; not overwritten'}
    @{schemaVersion='commissioning-desktop-profile/1';profileId=$profileId;expectedSubjectId=('commissioning:station01:'+$name);expectedRole=$entry[1];mode='RealDeviceCommissioning';credentialEnvironmentVariable=('GAODE_COMMISSIONING_'+$name.ToUpperInvariant()+'_CREDENTIAL');preparedTemplatePath=$template;logRoot=(Join-Path $root 'data/desktop-logs')}|ConvertTo-Json|Set-Content -LiteralPath $path -Encoding utf8
    $profiles+=$path
}
@{Gaode=@{CommissioningIdentities=$identities}}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $private 'host-identities.json') -Encoding utf8
if(Test-Path -LiteralPath $template){throw 'Business template exists; not overwritten'}
@{schemaVersion='commissioning-console-template/1';id='station01-console';version='1';sourceReference=('installed-recipe:'+$definition.recipeId+'/'+$definition.version);mode='RealDeviceCommissioning';contextTemplate=@{schemaVersion='station01-start-run-context/2.0';stationId='station01';lineId='line01';scenarioId=$definition.scenarioId;occupiedSlots=@($definition.positions.slotId);purpose='Commissioning'};publicConfigRef=$inputs.publicConfigRef;budgetRef=$inputs.budgetRef;simulationRef=@{id='station01-virtual-algorithm';version='1'}}|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $template -Encoding utf8
@{schemaVersion='commissioning-identity-preparation/1';roles=@('Operator','ProcessEngineer');profiles=$profiles;businessTemplate=$template;privateDirectory=$private;credentialValuesOmitted=$true;accessRestrictedToCurrentUser=$true;hardwareConnected=$false;motionAuthorized=$false}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $root 'data/identity-preparation.json') -Encoding utf8
Write-Output 'Two local controlled identities and desktop profiles prepared; no credentials printed, no devices started.'
