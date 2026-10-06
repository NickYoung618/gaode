"""Declare independent Test inputs, retain PNG pixel data and process its explicit sample annotation."""
from pathlib import Path
import copy, json, shutil, hashlib, struct, zlib
repo=Path(__file__).resolve().parents[1]
source=repo/'specs/014-special-part-rotation/examples/software-joint'
target=repo/'specs/016-public-preparation-tray-check-unload/examples'
def write(path,value):
    path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def annotation(data,value):
    result=data[:8];p=8
    while p<len(data):
        n=struct.unpack('>I',data[p:p+4])[0];kind=data[p+4:p+8];chunk=data[p+8:p+8+n]
        if kind==b'tEXt' and chunk.startswith(b'gaode010.sample.v1\0'):
            chunk=b'gaode010.sample.v1\0'+json.dumps(value,separators=(',',':')).encode('ascii')
        result+=struct.pack('>I',len(chunk))+kind+chunk+struct.pack('>I',zlib.crc32(kind+chunk)&0xffffffff);p+=n+12
    return result
catalog=json.loads((source/'catalog.json').read_text(encoding='utf-8-sig'))
ordinary=copy.deepcopy(catalog['definitions'][0]);ordinary['schemaVersion']='recipe-definition/5'
ordinary['capacity']=3;ordinary['positions']=[];ordinary['executionPositions']={}
base=catalog['definitions'][0];cells=[{'cellId':'r2:c4','row':2,'column':4,'region':'OK'}, {'cellId':'r2:c5','row':2,'column':5,'region':'OK'}, {'cellId':'r2:c6','row':2,'column':6,'region':'OK'}]
ordinary['trayLayout']['cells']=[c for c in ordinary['trayLayout']['cells'] if c['region']!='OK']+cells
ordinary['trayLayout']['cells'] += [{'cellId':'r2:c8','row':2,'column':8,'region':'Pending'}, {'cellId':'r2:c9','row':2,'column':9,'region':'Pending'}]
ordinary['pendingCapacity']=3
for cell,x in [('r2:c8',410),('r2:c9',380)]:
    target_point=copy.deepcopy(ordinary['sortingTargets']['r2:c10'])
    target_point['point'].update(id='016-Pending-'+cell,x=x)
    target_point['coordinateEvidenceReference']='Test:016-declared-pending-points/1'
    ordinary['sortingTargets'][cell]=target_point
ordinary['traySlotMapping']['bindings']=[]
for slot,cell in zip([1,2,3],cells):
    p=copy.deepcopy(base['positions'][0]);p.update(slotId=f's{slot}',physicalSlotIndex=slot,cellId=cell['cellId'],unitPattern='{TrayRunId}/s'+str(slot));ordinary['positions'].append(p)
    entry=copy.deepcopy(base['executionPositions']['s1']);entry['slotId']=f's{slot}'
    for object_input in [entry['physicalEntity'],*entry.get('members',{}).values()]:
        for c in object_input['coordinates']:
            c.update(slotId=f's{slot}',physicalSlotIndex=slot,sourceFactReference='Test:016-declared-detection-points/1')
            c['point']['x']={1:110,2:130,3:150}[slot]
            c['point']['id']='016-detection-'+str(slot)+'-'+c['camera']
        object_input['source']['point']['x']+=20*(slot-1)
        object_input['source']['point']['id']='016-origin-s'+str(slot)
        object_input['source']['coordinateEvidenceReference']='Test:016-declared-grab-points/1'
        object_input['sortingCellIds']['Pending']={1:'r2:c10',2:'r2:c9',3:'r2:c8'}[slot]
    ordinary['executionPositions'][f's{slot}']=entry
    ordinary['traySlotMapping']['bindings'].append({'cellId':cell['cellId'],'physicalSlotIndex':slot})
ordinary['approval']['allowedSlots']=['s1','s2','s3']
for case,presence,tilts in [('mixed',[True,False,True],[0,0,20]),('empty',[False,False,False],[0,0,0]),('intervention',[True,False,True],[0,0,20]),('all-abnormal',[True,True,True],[20,20,20])]:
    root=target/case;shutil.copytree(source/'config',root/'config',dirs_exist_ok=True)
    for name in ['worker.json','mechanics.json']:shutil.copyfile(source/name,root/name)
    shutil.copytree(source/'media',root/'media',dirs_exist_ok=True)
    write(root/'catalog.json',{'schemaVersion':'recipe-catalog-snapshot/1','definitions':[ordinary]})
    sample={'traySlots':[dict(physicalSlotIndex=i,present=present,tiltDegrees=tilt,**cells[i-1]) for i,present,tilt in zip([1,2,3],presence,tilts)],
            'expectedPhysicalSlotIndices':[1,2,3],'mappingSourceReference':'Test:016-explicit-3D-map/1'}
    if case!='empty':sample['fMarker']={'xMicrometres':125000,'yMicrometres':100000,'frame':'SIM_MACHINE'}
    (root/'media/ThreeD.png').write_bytes(annotation((source/'media/ThreeD.png').read_bytes(),sample))
    images=json.loads((source/'images-ordinary.json').read_text(encoding='utf-8-sig'));images['captureDelayMs']=3000
    for image in images['images']:image['sha256']=hashlib.sha256((root/image['relativePath']).read_bytes()).hexdigest().upper()
    write(root/'images.json',images)
    run=json.loads((source/'run-ordinary.json').read_text(encoding='utf-8-sig'));run.update(caseId='016-'+case,occupiedSlots=['s1','s2','s3'],imageManifestPath='images.json')
    run['recipeInputSha256']=hashlib.sha256((root/'catalog.json').read_bytes()).hexdigest().upper()
    run['restrictions']=['Explicit Test input: simulated PNG camera and lights, ContentSampleWorker process, TCP VirtualPlc and real SQLite; no production claim.']
    run['expected']={'endReason':'EmptyTray' if case=='empty' else 'ManualIntervention' if case=='intervention' else 'NormalCompletion',
        'inspectionCompleted':False,'productCaptureCount':2 if case=='mixed' else 0,
        'sortingTransferCount':1 if case=='mixed' else 3 if case=='all-abnormal' else 0,
        'fRequired':case in ('mixed','all-abnormal'),'coverage':'Complete','physicalSlots':[1,2,3]}
    run.pop('saveIsolation',None)
    write(root/'run.json',run)
print('Created four explicitly mapped Test cases')
