import json, hashlib
from pathlib import Path

repo = Path(r'D:\gaode')
root = Path(r'D:\Gaode-Station01\commissioning-021-final-3')
dest = root / 'data/config/runtime-r1'
dest.mkdir(exist_ok=True)
def read(p): return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def write(name, value): (dest/name).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
oldroot=Path(r'D:\Gaode-PlcCommissioning-20261006\release\Gaode-PlcCommissioning-1.1.6-win-x64\config')
old=read(oldroot/'recipe.local.json'); link=read(oldroot/'local.json')
saved=read(root/'data/recipe-readback.json'); definition=saved['recipes'][0]['definition']
inputs=read(root/'data/config/commissioning.json'); frame=definition['executionPositions']['s1']['physicalEntity']['source']['point']['frame']
purpose='RealDeviceCommissioning'
source='User-confirmed 2026-10-08 mm/XY0..100/Z0..10/startupXYZ0; existing successful local recipe sha256:'+hashlib.sha256((oldroot/'recipe.local.json').read_bytes()).hexdigest()
captures={c['role']:c for c in read(repo/'artifacts/camera-runtime/seven-after-captures.json')['cameras']}
public=read(repo/'backend/tests/Gaode.Communication.Tests/Fixtures/CameraReview/public.test.json')
public.update(id=inputs['publicConfigRef']['id'],version=inputs['publicConfigRef']['version'],purpose=purpose,source=source)
public['bindings']=[dict(id='site-plc',provider='Real',role='PLC')]+[dict(id=r,provider='Real',role=('Camera3D' if r=='3D' else 'Camera'+r)) for r in ['3D','F']]+[dict(id='virtual-'+r,provider='Simulated',role=r) for r in ['Light3D','LightF','TrayPose','FDecode']]
def point(step,id):
 s=next(s for s in old['steps'] if s['id']==step)
 return dict(id=id,version='local-1.1.6',x=s['x'],y=s['y'],z=0,unit='mm',frame=frame)
public['motion'].update(capability=dict(id='xyz.fixed',contractVersion='1.0'),axes=['X','Y','Z'],frame=frame,unit='mm',limits=dict(xMin=0,xMax=100,yMin=0,yMax=100,zMin=0,zMax=10),points=dict(threeD=point(2,'site-3d'),f=point(3,'site-f'),unload=point(14,'site-unload')),positionTolerance=old['positionTolerance'],coordinateSource=source)
public['motion']['coordinateDigest']=hashlib.sha256(json.dumps(public['motion']['points'],sort_keys=True).encode()).hexdigest().upper()
public['capture3d'].update(bindingId='3D',lightBindingId='virtual-Light3D',scope=dict(id='site-whole-tray',version='local-1.1.6',kind='WholeTray',unit='mm',frame=frame,bounds=dict(xMin=0,xMax=100,yMin=0,yMax=100)),parameters=dict(exposureUs=int(captures['3D']['metadata']['actualParameters']['IR_Exposure'])*1000,lightLevel=0),maxCaptureBytes=captures['3D']['maxBytes'])
public['captureF'].update(bindingId='F',lightBindingId='virtual-LightF',parameters=dict(exposureUs=int(captures['F']['metadata']['actualParameters']['ExposureTime']),lightLevel=0),maxCaptureBytes=captures['F']['maxBytes'])
public['algorithms']=dict(fDecode=dict(capability=dict(id='code.raw-candidates',contractVersion='1.0'),bindingId='virtual-FDecode',parametersVersion='1'),trayPose=dict(capability=dict(id='tray.observation',contractVersion='1.0'),bindingId='virtual-TrayPose',parametersVersion='1'))
public['parser']=dict(capability=dict(id='decoded-content-exact',contractVersion='1.0'),parametersVersion='1')
public['lightExecution']=dict(schemaVersion='light-execution/1',mode='Simulated')
for capture in ['capture3d','captureF']: public[capture]['parameters'].pop('lightLevel')
write('public.json',public)
schema=read(repo/'specs/001-station01-public-preparation/contracts/public-config.schema.json')
schema['properties']['lightExecution']=dict(type='object',additionalProperties=False,required=['schemaVersion','mode'],properties=dict(schemaVersion=dict(const='light-execution/1'),mode=dict(type='string',enum=['Simulated','Real'])))
for capture in ['capture3d','captureF']: schema['properties'][capture]['properties']['parameters']['required'].remove('lightLevel')
(repo/'specs/021-commissioning-console/contracts/public-config.runtime.schema.json').write_text(json.dumps(schema,ensure_ascii=False,indent=2),encoding='utf-8')
schema_root=dest/'schemas'; schema_root.mkdir(exist_ok=True)
for p in (root/'config/schemas').glob('*.json'): (schema_root/p.name).write_bytes(p.read_bytes())
(schema_root/'public-config.schema.json').write_text(json.dumps(schema,ensure_ascii=False,indent=2),encoding='utf-8')
budget=read(repo/'backend/tests/Gaode.Communication.Tests/Fixtures/CameraReview/budgets.test.json')
budget.update(id=inputs['budgetRef']['id'],version=inputs['budgetRef']['version'],purpose=purpose,source='Commissioning engineering runtime-r1; motion deadline from local.json actionTimeout=30s; camera deadlines from RealCameraOptions=30s; virtual algorithm/software allowances explicitly configured, not measured field performance; queue defaults from existing CameraReview budget')
budget['businessMs'].update(plcAcceptance=3000,clampCompletion=30000,xyCompletion=30000,capture3d=30000,captureF=30000,trayPoseAlgorithm=1000,fDecode=1000,flipCompletion=30000,putBackCompletion=30000,plcIo=1000,workerReleaseGrace=15000,recipeApplication=1200000)
budget['limits'].update(fReservedMemoryBytes=captures['F']['maxBytes'],mediaMemoryBytes=268435456,runMediaQuotaBytes=2147483648,dataQuotaBytes=8589934592)
budget['recipeExecution']=dict(captureMs=30000,algorithmMs=1000,acquisitionReleaseMs=15000,captureWaitMs=30000,algorithmWaitMs=1000,inputReleaseWaitMs=15000)
write('budget.json',budget)
pose=definition['executionPositions']['s1']['physicalEntity']['flip']['stages']['stage:2']['targetPose']
write('plc-mechanics.json',dict(schemaVersion='plc-mechanics/1',purpose=purpose,sourceReference=source,sortingSafePosition=None,positionBasis=dict(frame=frame,unit='mm',purpose=purpose,sourceReference=source),siteOperations=read(root/'config/site-operations-confirmed-20261008.json'),posePrograms=[dict(model=definition['model'],**pose,targetFaceWord=next(s['targetFace'] for s in old['steps'] if s['kind']=='flip'),modelWords=[],modelNumber=old['modelCode'],sourceReference=source,purpose=purpose)]))
write('plc-field.json',dict(layoutId='confirmed-20261006-v2',confirmed=True,source=str(oldroot/'local.json')+'; effective read/write Cdab; PC.xls/PLC(2).xls confirmed memory layout',pcPduBase=link['pcBase'],plcPduBase=link['plcBase'],plcArea='HoldingRegister',boolByteOrder=link['boolOrder'],floatOrder=link['realReadOrder']))
def path(p): return str(root/p)
settings=dict(Urls='http://127.0.0.1:5190',Gaode=dict(Mode=purpose,TestRoot=path('data/store'),AllowedTestRoot=path('data'),ConfigRoot=str(dest),SchemaRoot=path('config/schemas'),PublicId=inputs['publicConfigRef']['id'],PublicVersion='1',BudgetId=inputs['budgetRef']['id'],BudgetVersion='1',CommissioningId=inputs['id'],CommissioningVersion='1',CommissioningPath=path('data/config/commissioning.json'),CommissioningSha256=read(root/'data/commissioning-inputs.json')['sha256'],PlcHost=link['host'],PlcPort=link['port'],PlcUnitId=link['unitId'],PlcProvider='Real',PlcIoTimeoutMs=1000,PositionTolerance=old['positionTolerance'],PlcMechanicsPath=str(dest/'plc-mechanics.json'),PlcFieldProfilePath=str(dest/'plc-field.json'),Cameras=dict(Enabled=True,SitePath=path('config/camera.site.json'),WorkerPath=path('app/worker/Gaode.CameraWorker.exe'),GalaxySdkPath=r'D:\GalaxySDK\APIDll\Win64',CameraProSdkPath=r'D:\软件开发sdk\3DCameraViewer\CamSDK\CamSDK_CSharp\bin',StateRoot=path('data/camera-state'),StartupTimeoutMs=30000,CaptureTimeoutMs=30000,ShutdownTimeoutMs=15000)),RecipeStore=dict(DatabasePath=path('data/recipes/recipes.db'),ReadWriteTimeoutMs=2000,DbLockTimeoutSeconds=2))
settings['Gaode']['SchemaRoot']=str(schema_root)
write('hostsettings.json',settings)
write('sources.json',dict(schemaVersion='commissioning-runtime-sources/1',scope='single-part AB flip CD OK; seven real cameras; virtual algorithm and lights',source=source,networkAccess=False,deviceDispatches=0,fieldValidated=False,differences=['I/O timeout reduced from old tool 2000ms to formal maximum 1000ms; no retry','Motion deadline 30s sourced; camera timeout 30s existing driver default; remaining software allowances are engineering commissioning settings','Whole-tray bounds constrained to confirmed XY envelope; virtual single cell only; no physical tray geometry/calibration claim','F/3D exposures and payload capacities from actual historical captures; 3D SDK ms converted to us','Sorting safety absent; NG/Pending must remain blocked; E not in current recipe; no automatic homing'],files=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest().upper()) for p in dest.rglob('*.json') if p.name not in ('sources.json','preflight-result.json')]))
print(json.dumps(dict(configurationRoot=str(dest),profileActivated=False,deviceDispatches=0)))
