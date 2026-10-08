"""Convert the known local flip recipe to the formal saved-body input; no device IO."""
import argparse,copy,hashlib,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--source',required=True);p.add_argument('--output',required=True);p.add_argument('--camera-evidence',required=True);a=p.parse_args()
out=Path(a.output)
if out.exists():raise SystemExit('Use a fresh output directory')
out.mkdir(parents=True)
source=Path(a.source);raw=source.read_bytes();old=json.loads(raw.decode('utf-8-sig'))
if old['cameraMode']!='simulated' or old['simulation']['pose']!='OK' or any(old['simulation'][k]!='OK' for k in 'ABCD'):raise SystemExit('Unexpected source scope')
steps=old['steps'];photos=[s for s in steps if s['kind']=='photo']
if [(s['face'],s['camera']) for s in photos]!=[(1,'A'),(1,'B'),(2,'C'),(2,'D')]:raise SystemExit('Unexpected face order')
evidence=Path(a.camera_evidence);cams={c['role']:c for c in json.loads(evidence.read_text(encoding='utf-8-sig'))['cameras']}
base=Path(__file__).resolve().parent.parent/'configuration/recipe-authoring/ordinary-source.json'
r=json.loads(base.read_text(encoding='utf-8-sig'));frame='COMMISSIONING_MACHINE';version='local-1.1.6';ref=f'{source}:sha256:{hashlib.sha256(raw).hexdigest()}'
r.update(schemaVersion='recipe-definition/5',scenarioId='commissioning-single-flip',model=old['name'],fCode=old['simulation']['barcode'],layoutProfile='commissioning-single-part',motionProfile='commissioning-flip',qualityProfile='commissioning-virtual-ok',inspectionKind='ordinary',sortingGripperId=old.get('grabId') or 1)
r['composition'][0]['localFaces']=[1,2]
r['traySlotMapping'].update(id='commissioning-single-slot-map',version=version,evidenceReference=ref+'; one part, software cell r2:c4 maps slot1')
r['stages']=[dict(number=n,action='none' if n==1 else 'flipAffectedMembersOneByOne',angleDeg=None,targets=[dict(material='part',localFace=n,cameraPair=pair,captureProfile='capture-'+pair[0],algorithmProfile='defect')]) for n,pair in [(1,'AB'),(2,'CD')]]
r['algorithmRequirements']={'defect':dict(id='defect',parametersVersion='commissioning-virtual/1',purpose='SingleDetection',inputCount=1,resultContract='image-quality/1'),'defect/fusion':dict(id='defect/fusion',parametersVersion='commissioning-virtual/1',purpose='FaceFusion',inputCount=2,resultContract='face-quality/1')}
r['captureProfiles']={}
def point(id,x,y,z=0):return dict(id=id,version=version,x=x,y=y,z=z,unit='mm',frame=frame)
def planar(id,x,y):d=point(id,x,y);del d['z'];return d
def fixed(z):return dict(z=z,unit='mm',datum=frame,approvalReference=ref,configurationVersion=version)
coordinates=[]
for s in photos:
 c=s['camera'];meta=cams[c]['metadata'];parameters=meta['actualParameters'];profile='capture-'+c
 r['captureProfiles'][profile]=dict(id=profile,version=version,settings=dict(profileId=profile,exposureUs=int(float(parameters['ExposureTime'])),gain=1,roiPixels=[0,0,meta['width'],meta['height']],lightChannel=None,brightnessPercent=None,settleMs=None))
 coordinates.append(dict(pointRef='face'+str(s['face'])+'-'+c,point=planar('detect-'+c,s['x'],s['y']),objectPattern='{UnitId}/part',slotId='s1',physicalSlotIndex=1,localFace=s['face'],camera=c,stageId='stage:'+str(s['face']),configurationVersion=version,sourceFactReference=ref+':step:'+str(s['id']),fixed=fixed(s['z']),captureProfile=profile))
physical=r['executionPositions']['s1']['physicalEntity'];sorting=old['sorting']
physical.update(source=dict(point=point('source',sorting['sourceX'],sorting['sourceY'],sorting['pickZ']),coordinateEvidenceReference=ref+':sorting-source'),coordinates=coordinates,flip={'stages':{'stage:2':dict(targetPose=dict(profileId=r['motionProfile'],profileVersion=version,poseKey='face-2'),pickPointRef='flip-pick',putBackPointRef='flip-put')}},rotation=None,purposePoints={})
for kind,key,purpose in [('position','flip-pick','FlipPick'),('position','flip-put','FlipPutBack')]:
 s=next(s for s in steps if s['kind']==kind and s['id']==(6 if key=='flip-pick' else 8))
 physical['purposePoints'][key]=dict(purpose=purpose,point=planar(key,s['x'],s['y']),fixed=fixed(0),coordinateEvidenceReference=ref+':step:'+str(s['id'])+'; XY-only Z0 unused')
r['sortingTargets']={cell:dict(point=point(name,sorting[x],sorting[y],sorting['placeZ']),coordinateEvidenceReference=ref+':sorting-unused-in-OK') for cell,name,x,y in [('r2:c1','NG-1','ngX','ngY'),('r2:c10','Pending-1','pendingX','pendingY')]}
f=next(s for s in steps if s['kind']=='scan');r['commissioningFPosition']=dict(schemaVersion='commissioning-f-position/1',x=f['x'],y=f['y']);r['lightExecution']=dict(schemaVersion='light-execution/1',mode='Simulated')
# Base is a document-shape template only: no baseline sample coordinates or grant survives.
for k in ('recipeId','version','definitionDigest','catalogDigest'):r[k]=''
r['plcRecipeId']=None;r['approval']=dict(id='',version='',digest='',purpose='',allowedSlots=[],evidenceReference='');r['releaseStatus']='draft'
source_map=dict(schemaVersion='commissioning-recipe-conversion/1',sourcePath=str(source),sourceSha256=hashlib.sha256(raw).hexdigest(),cameraEvidencePath=str(evidence),cameraEvidenceSha256=hashlib.sha256(evidence.read_bytes()).hexdigest(),currentRoute='single part AB->flip->putback->3D review->CD->OK original slot',fXY=[f['x'],f['y']],modelNumber=old['modelCode'],sourceOnly=True,authorizesHardware=False,placeholderPolicy=[dict(field='sortingGripperId',value=r['sortingGripperId'],reason='source missing; user-authorized inactive placeholder; fixed OK performs no sorting; sorting safety config remains absent'),dict(field='captureProfiles.*.gain',value=1,reason='user-authorized imaging initial value; must be tuned; no motion semantics'),dict(field='purposePoints.*.fixed.z',value=0,reason='formal site protocol XY only; no Z request'),dict(field='lightExecution',value='Simulated',reason='explicit skip all external-light parameters/control'),dict(field='eCode.enabled',value=False,reason='source route does not scan E')],remainingFieldReview=['PLC first/recovery safety semantics','mechanical travel limits and frame basis','sorting gripper before enabling any NG/Pending route','camera gain/exposure quality','slot mapping before multi-part use'])
for name,obj in [('recipe-source.json',r),('recipe-source-map.json',source_map),('original-recipe.local.json',old)]: (out/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(output=str(out),fXY=[f['x'],f['y']],photoCount=len(photos),networkAccess=False),ensure_ascii=False))
