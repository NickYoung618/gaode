param([Parameter(Mandatory)][string]$InstallationRoot)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($InstallationRoot)
$readback=Get-Content -LiteralPath (Join-Path $root 'data/recipe-readback.json') -Raw|ConvertFrom-Json
if(@($readback.recipes).Count -ne 1){throw '初始配置需要唯一已保存联调配方。'}
$saved=$readback.recipes[0]
$definition=$saved.definition
$source=Get-Content -LiteralPath (Join-Path $root 'config/recipe-source-map.json') -Raw|ConvertFrom-Json
$reference='local-recipe:'+ $source.sourceSha256 + '; user-confirmed single flip OK route'
$publicRef=@{id='station01-public-commissioning';version='1'}
$budgetRef=@{id='station01-budget-commissioning';version='1'}
$results=@()
$captures=@($saved.plan.steps|Where-Object kind -eq 5)
foreach($step in $captures){
    $scope=@{slotId=$step.slotId;material=$step.material;stageId=$step.stageId;localFace=$step.localFace;heightRound=$step.coordinateEpoch;camera=$step.camera}
    $results+=@{id=('single-'+$step.sequence);version='1';source=$reference;purpose=2;scope=$scope;disposition='OK'}
}
foreach($group in @($captures|Group-Object slotId,material,stageId,localFace,coordinateEpoch)){
    $step=$group.Group[0]
    $scope=@{slotId=$step.slotId;material=$step.material;stageId=$step.stageId;localFace=$step.localFace;heightRound=$step.coordinateEpoch;camera=(@($group.Group.camera|Sort-Object)-join '')}
    $results+=@{id=('fusion-'+$step.stageId);version='1';source=$reference;purpose=3;scope=$scope;disposition='OK'}
}
$slots=@($definition.positions|ForEach-Object{
    $position=$_;$cell=$definition.trayLayout.cells|Where-Object cellId -eq $position.cellId
    @{physicalSlotIndex=$position.physicalSlotIndex;presence='Present';pose='Normal';reason='用户指定单品OK联调';cellId=$cell.cellId;region='OK';row=$cell.row;column=$cell.column}
})
$input=@{
    schemaVersion='020-commissioning/1';id='station01-virtual-algorithm';version='1';purpose='RealDeviceCommissioning';source=$reference
    publicConfigRef=$publicRef;budgetRef=$budgetRef
    expectedRecipe=@{recipeId=$definition.recipeId;version=$definition.version;definitionDigest=$definition.definitionDigest;scenarioId=$definition.scenarioId;model=$definition.model;fCode=$definition.fCode}
    codeRule=@{id='decoded-content-exact';version='1.0';source='用户指定当前已保存配方的料盘码，按正式F绑定核对'}
    publicLightChannels=@{}
    algorithms=@(
        @{purpose=1;capabilityId='code.raw-candidates';capabilityVersion='1.0';resultContract='decoded-code/1';inputCount=1;parametersVersion='1'},
        @{purpose=5;capabilityId='tray.observation';capabilityVersion='1.0';resultContract='tray-observation/2';inputCount=1;parametersVersion='1'},
        @{purpose=2;capabilityId='detection.single';capabilityVersion='1.0';resultContract='image-quality/1';inputCount=1;parametersVersion='commissioning-virtual/1'},
        @{purpose=3;capabilityId='detection.fusion';capabilityVersion='1.0';resultContract='face-quality/1';inputCount=2;parametersVersion='commissioning-virtual/1'})
    slots=$slots
    fLocation=@{x=$definition.commissioningFPosition.x;y=$definition.commissioningFPosition.y;unit='mm';frame=$definition.executionPositions.s1.physicalEntity.coordinates[0].point.frame;sourceReference=('saved-recipe:'+ $definition.recipeId+'/'+$definition.version)}
    mappingSourceReference=$definition.traySlotMapping.evidenceReference;rawCodes=@($definition.fCode);results=$results
}
$configRoot=Join-Path $root 'data/config'
[IO.Directory]::CreateDirectory($configRoot)|Out-Null
$path=Join-Path $configRoot 'commissioning.json'
if(Test-Path -LiteralPath $path){throw '受控虚拟输入已存在，不覆盖。配方编辑后须按新版本重建输入。'}
$input|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $path -Encoding utf8NoBOM
@{schemaVersion='commissioning-input-preparation/1';source=$reference;recipeId=$definition.recipeId;version=$definition.version;
    definitionDigest=$definition.definitionDigest;path=$path;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;
    publicConfigRef=$publicRef;budgetRef=$budgetRef;networkAccess=$false;motionAuthorized=$false;scope='single-part-two-face-OK; public media must still be acquired'
}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $root 'data/commissioning-inputs.json') -Encoding utf8
