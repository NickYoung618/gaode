"""Build a separate Q01-PARAM Test catalog; never rewrite the Q01 baseline."""
import copy
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parent
version = '1.2.0-test'
mapping = 'test-virtual-mapping/1.0.1'

def read(name):
    return json.loads((root / name).read_text(encoding='utf-8'))

def write(name, value):
    path = root / name
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return path

catalog = read('recipes.json')
recipe = catalog['recipes'][0]
assert recipe['recipeId'] == 'R008-Q01' and recipe['version'] == '1.1.1-test'
assert recipe['fCode']['virtualExactPayload'] == 'TEST-TRAY-0101'
recipe['version'] = version
recipe['model'] = 'Q01-PARAM-Test'
recipe['testEligibleSlots'] = ['P01', 'P03']
catalog['source']['basis'] = 'Q01-PARAM Test: existing Q01 plus explicit P03 AB and changed capture/worker parameters'
base_capture = catalog['virtualFixedProfiles']['SIM_CAPTURE_AB']
changed_capture = copy.deepcopy(base_capture)
changed_capture.update(exposureUs=12000, brightnessPercent=75, roiPixels=[0, 0, 960, 960], settleMs=150)
catalog['virtualFixedProfiles']['SIM_CAPTURE_AB_PARAM'] = changed_capture
changed_algorithm = copy.deepcopy(catalog['virtualFixedProfiles']['SIM_ALGORITHM'])
changed_algorithm['modelVersion'] = 'SIM_ONLY_V2_PARAM'
catalog['virtualFixedProfiles']['SIM_ALGORITHM_PARAM'] = changed_algorithm
for member in recipe['compositionPerOccupiedSlot']:
    member['captureProfile'] = 'SIM_CAPTURE_AB_PARAM'
    member['algorithmProfile'] = 'SIM_ALGORITHM_PARAM'
for stage in recipe['execution']['stages']:
    for target in stage['targets']:
        target['captureProfile'] = 'SIM_CAPTURE_AB_PARAM'
        target['algorithmProfile'] = 'SIM_ALGORITHM_PARAM'
position = next(p for p in recipe['positions'] if p['slotId'] == 'P03')
position['protocolSlotIndex'] = 3
position['pointRefs'] = {c: f'Q01-PARAM-P03-{c}-{mapping}' for c in 'AB'}
position['resolvedSourcePoint'] = {
    'id': 'P03', 'version': mapping, 'x': 300, 'y': 100, 'z': 150,
    'unit': 'mm', 'frame': 'SIM_MACHINE',
    'testSourceRef': f'Test/VirtualSource/Q01-PARAM/P03/{mapping}',
    'point': {'id': 'P03', 'version': mapping, 'x': 300, 'y': 100, 'z': 150,
              'unit': 'mm', 'frame': 'SIM_MACHINE'}}
position['resolvedDetectionTargets'] = {}
for camera, dx in [('A', 0), ('B', 10)]:
    position['resolvedDetectionTargets'][camera] = {
        'pointRef': position['pointRefs'][camera], 'zMode': 'TestHeightOffset',
        'testSourceRef': f'Test/VirtualPoint/Q01-PARAM/P03/{camera}/{mapping}',
        'point': {'id': f'Q01-PARAM-P03-{camera}', 'version': mapping,
                  'x': 300 + dx, 'y': 100, 'unit': 'mm', 'frame': 'SIM_MACHINE'},
        'heightBinding': {'scopeId': 'sim-whole-tray', 'scopeVersion': '1.0.0',
                          'sampleId': 'sample-b', 'objectSlotId': 'P03',
                          'localFace': 1, 'heightRound': 1, 'unit': 'mm',
                          'datum': 'SIM_REFERENCE', 'offsetMm': 100.0,
                          'minZ': 105.0, 'maxZ': 120.0}}
catalog_path = write('recipes-q01-param.json', catalog)
digest = hashlib.sha256(catalog_path.read_bytes()).hexdigest().upper()
media = read('media-manifest.json')
media['id'] = 'q01-param-simulated-media'
media['version'] = version
write('media-manifest-q01-param.json', media)
worker = read('worker-manifest.json')
worker['id'] = 'q01-param-virtual-algorithm'
worker['version'] = version
worker['seed'] = 7031
assert worker['fCode'] == 'TEST-TRAY-0101'
write('worker-manifest-q01-param.json', worker)
fixture = read('fixture.json')
fixture['caseId'] = 'Q01-PARAM'
fixture['occupiedSlots'] = ['P01', 'P03']
fixture['recipeRef']['version'] = version
fixture['recipeRef']['catalogDigest'] = digest
for key, name in [('recipeCatalogPath', 'recipes-q01-param.json'),
                  ('imageManifestPath', 'media-manifest-q01-param.json'),
                  ('workerManifestPath', 'worker-manifest-q01-param.json')]:
    fixture[key] = str((root / name).resolve())
write('fixture-q01-param.json', fixture)
cases = read('cases.json')
cases['cases'] = [row for row in cases['cases'] if row['caseId'] != 'Q01-PARAM']
case = copy.deepcopy(next(row for row in cases['cases'] if row['caseId'] == 'Q01'))
case['caseId'] = 'Q01-PARAM'
case['recipeRef']['version'] = version
case['recipeRef']['catalogDigest'] = digest
case['occupiedSlots'] = ['P01', 'P03']
first = case.pop('object')
second = copy.deepcopy(first)
second['sourceSlotId'] = 'P03'
second['unitIdPattern'] = '{TrayRunId}:P:P03'
second['pointRefs'] = position['pointRefs']
second['protocolSlotIndex'] = 3
second['heightBinding'] = copy.deepcopy(position['resolvedDetectionTargets']['A']['heightBinding'])
case['objects'] = [first, second]
case['mediaInputs']['A'] = 'media-manifest-q01-param.json#Detection/A'
case['mediaInputs']['B'] = 'media-manifest-q01-param.json#Detection/B'
cases['cases'].append(case)
write('cases.json', cases)
print(json.dumps({'caseId': 'Q01-PARAM', 'version': version, 'catalogDigest': digest,
                  'slots': recipe['testEligibleSlots'], 'captureProfile': 'SIM_CAPTURE_AB_PARAM',
                  'workerSeed': worker['seed']}))