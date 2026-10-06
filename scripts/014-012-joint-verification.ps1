param([ValidateSet('special','ordinary')][string]$Case,[string]$Attempt=(Get-Date -Format 'yyyyMMdd-HHmmss'))
$ErrorActionPreference='Stop'
$jointRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if($jointRepo -ne 'E:\dzk\gaode-1'){throw 'CurrentMainWorkspaceRequired'}
if(-not $Case){throw 'One declared representative case required'}
$jointAllowed=Join-Path $jointRepo 'artifacts/014-012-joint'
$jointControl=Join-Path $jointAllowed ('control-'+$Case+'-'+$Attempt)
if(Test-Path -LiteralPath $jointControl){throw 'FreshEvidenceRequired'}
New-Item -ItemType Directory -Path $jointControl | Out-Null
$jointRun=Join-Path $jointAllowed ($Case+'-'+$Attempt)
$jointPage=Join-Path $jointAllowed ('pages/'+$Case+'-'+$Attempt)
$env:SPECIFY_FEATURE_DIRECTORY='specs/014-special-part-rotation'
if(-not(Test-Path (Join-Path $jointRepo ($env:SPECIFY_FEATURE_DIRECTORY+'/tasks.md')))){throw '014TasksMissing'}
$env:GAODE_014_JOINT_ROOT=$jointAllowed
$env:GAODE_011_FULLRUN_ROOT=$jointRun
$env:GAODE_011_FIXTURE=Join-Path $jointRepo ('specs/014-special-part-rotation/examples/software-joint/run-'+$Case+'.json')
$env:GAODE_011_PAGE_EVIDENCE_ROOT=$jointPage
$env:GAODE_011_PYTHON=(Get-Command python).Source
$env:PYTHONUTF8='1'
$env:PYTHONDONTWRITEBYTECODE='1'
# No 013 measurement mode or backend-only observer. The page reads the same real run.
if($env:GAODE_013_ATTEMPT_ROOT){throw '013MeasurementEnvironmentNotAllowed'}
$jointConnection=Join-Path $jointRun 'page-connection.json'
$env:SPECIFY_FEATURE_DIRECTORY='specs/012-recipe-authoring'
if(-not(Test-Path (Join-Path $jointRepo ($env:SPECIFY_FEATURE_DIRECTORY+'/tasks.md')))){throw '012TasksMissing'}
$jointObserver=Start-Process -FilePath (Get-Command node).Source -ArgumentList @((Join-Path $PSScriptRoot '014-012-joint-page.mjs'),$jointConnection,$jointControl) -WorkingDirectory $jointRepo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $jointControl 'page.out.log') -RedirectStandardError (Join-Path $jointControl 'page.err.log')
try {
 $jointBrowserDeadline=(Get-Date).AddSeconds(20)
 while(-not(Test-Path -LiteralPath (Join-Path $jointControl 'browser-prewarm.json'))){
  if($jointObserver.HasExited -or (Get-Date) -gt $jointBrowserDeadline){throw 'BrowserPreparationRequiredBeforeHost'}
  Start-Sleep -Milliseconds 100
 }
 $env:SPECIFY_FEATURE_DIRECTORY='specs/014-special-part-rotation'
 $env:GAODE_014_CONTROL=$jointControl
 & python -B -c 'import sys,json,hashlib,os;from pathlib import Path;sys.path.insert(0,str(Path("scripts/workflow").resolve()));import recipe_execution_010 as r;names=["backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll","backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll","backend/tests/Gaode.Integration.Tests/bin/Debug/net10.0/Gaode.Integration.Tests.dll","VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll"];r.write_json(Path(os.environ["GAODE_014_CONTROL"])/"source-build-before-run.json",dict(sourceFiles=r.source_files(),binaryFiles={n:hashlib.sha256(Path(n).read_bytes()).hexdigest() for n in names},fixturePath=os.environ["GAODE_011_FIXTURE"],fixtureSha256=r.sha(os.environ["GAODE_011_FIXTURE"])))'
 if($LASTEXITCODE -ne 0){throw 'ActualSourceAndBuildIdentityRequired'}
 & dotnet test (Join-Path $jointRepo 'backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj') --no-build --no-restore --filter 'FullyQualifiedName=Gaode.Integration.Tests.Station01.ThreeStageMainFlowIntegrationTests.CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite' --logger 'trx;LogFileName=joint.trx' --results-directory $jointControl *> (Join-Path $jointControl 'joint.log')
 $jointExit=$LASTEXITCODE
 if($jointExit -ne 0){
  [IO.File]::WriteAllText((Join-Path $jointControl 'observer-abort.json'),'{"state":"JointDriverFailed","businessSuccess":false}')
  $observerFinished=$jointObserver.WaitForExit(10000)
  [IO.File]::WriteAllText((Join-Path $jointControl 'driver-result.json'),(@{jointExit=$jointExit;observerFinished=$observerFinished;firstFailureLog='joint.log';accepted=$false}|ConvertTo-Json))
  throw 'JointRunFailed: retain the first test failure in joint.log; observer cleanup is not its cause'
 }
 if(-not $jointObserver.WaitForExit(45000)){throw 'SameRunObserverIncomplete'}
 if($jointExit -ne 0 -or $jointObserver.ExitCode -ne 0){throw 'JointEvidenceRejected: retain current logs/facts'}
} finally {
 if(-not $jointObserver.HasExited){
  [IO.File]::WriteAllText((Join-Path $jointControl 'observer-abort.json'),' {"state":"OwnedObserverCleanupRequested","businessSuccess":false}')
  if(-not $jointObserver.WaitForExit(10000)){Stop-Process -Id $jointObserver.Id -ErrorAction Continue}
 }
}
