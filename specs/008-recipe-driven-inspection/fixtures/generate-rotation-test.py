"""Virtual type-1 part/assembly routes from existing Test identities and source closed-loop.
Angles are the lower-device Review profile; no production PLC address is invented.
"""
import copy, hashlib, json
from pathlib import Path
root = Path(__file__).resolve().parent
def read(name): return json.loads((root/name).read_text(encoding='utf-8'))
def write(name, value):
    data = (json.dumps(value, ensure_ascii=False, indent=2)+'\n').encode('utf-8')
    (root/name).write_bytes(data)
    return hashlib.sha256(data).hexdigest().upper()
for assembly in (False, True):
    slug = 'rot-assembly' if assembly else 'rot-part'
    case = slug.upper()
    base = 'assembly-a-e' if assembly else 'q04'
    catalog = read(f'recipes-{base}.json')
    recipe = catalog['recipes'][0]
    recipe.update(recipeId=f'R008-{case}', version='1.0.0-test', plcRecipeId=206 if assembly else 205)
    f_code = f'TEST-TRAY-{recipe["plcRecipeId"]:04d}'
    recipe['fCode']['virtualExactPayload'] = f_code
    recipe['execution']['route'] = 'specialType1WholeAssembly' if assembly else 'specialType1Part'
    recipe['testEligibleSlots'] = ['P01']
    for index, stage in enumerate(recipe['execution']['stages']):
        stage.update(action='transferWholeAssemblyToRotationStationAndRotate' if index == 0 else 'rotateWholeAssembly',
                     fixedAngleDeg=90 if index == 0 else 180, coordinateRule='rotationStationCoordinates',
                     order='oneEntityThenAThenB')
    for position in recipe['positions']:
        if position['slotId'] != 'P01': continue
        objects = position['resolvedObjects'].values() if assembly else [position]
        for item in objects:
            for cameras in item['resolvedDetectionTargetsByFace'].values():
                for target in cameras.values():
                    target['point'].update(x=target['point']['x']+200, y=target['point']['y']+200,
                                           version='test-rotation/1.0.0')
                    target['point']['testSourceRef'] = f'Test/{case}/station/{target["point"]["id"]}/1.0.0'
                    target['coordinateConfigVersion'] = 'test-rotation/1.0.0'
                    target['zBasis']['mappingSourceRef'] = target['point']['testSourceRef']
        entry = copy.deepcopy(position['resolvedFlipPosition'])
        entry.update(testSourceRef=f'Test/{case}/station/entry/1.0.0', actionTimeoutMs=8000)
        entry['point'].update(x=300, y=300, version='test-rotation/1.0.0')
        poses = {f'pose{i}': copy.deepcopy(entry) for i in (1,2)}
        for pose_id, pose in poses.items():
            pose['testSourceRef'] = f'Test/{case}/station/{pose_id}/1.0.0'
        exits = {'OK': copy.deepcopy(position['resolvedSourcePoint']),
                 **copy.deepcopy(position['resolvedSortingTargets'])}
        for quality, exit_target in exits.items():
            exit_target.update(actionTimeoutMs=8000, testSourceRef=f'Test/{case}/exit/{quality}/1.0.0')
        position['resolvedRotation'] = dict(entry=entry, poses=poses, exits=exits,
            source='Type1 closed-loop; U03; lower-device Review pose1/pose2; Test budget XY=8000ms')
    catalog['source'].update(basis=f'recipe-cases C05 {case}; original type1 closed-loop; U03/U07; existing virtual identities',
                             status='TestOnly/Rotation', pointMapping='test-rotation/1.0.0')
    digest = write(f'recipes-{slug}.json', catalog)
    media = read(f'media-manifest-{base}.json')
    write(f'media-manifest-{slug}.json', media)
    for quality in ('OK','NG','Pending'):
        worker = read(f'worker-manifest-{base}.json')
        worker.update(id=f'{slug}-{quality.lower()}-worker', fCode=f_code, detectionDisposition=[quality])
        write(f'worker-manifest-{slug}-{quality.lower()}.json', worker)
        fixture = read(f'fixture-{base}.json')
        fixture.update(caseId=f'{case}-{quality.upper()}', occupiedSlots=['P01'], fCode=f_code)
        fixture['recipeRef'].update(recipeId=recipe['recipeId'], version=recipe['version'], catalogDigest=digest)
        fixture.update(recipeCatalogPath=str((root/f'recipes-{slug}.json').resolve()),
                       imageManifestPath=str((root/f'media-manifest-{slug}.json').resolve()),
                       workerManifestPath=str((root/f'worker-manifest-{slug}-{quality.lower()}.json').resolve()))
        write(f'fixture-{slug}-{quality.lower()}.json', fixture)
    print(json.dumps(dict(case=case, catalogDigest=digest, poses=2, exits=['OK','NG','Pending'])))
