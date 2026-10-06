"""Q04 manual Test derivative: existing SIM coordinates, protocol manual coils and U04."""
import copy, hashlib, json
from pathlib import Path
root = Path(__file__).resolve().parent
def read(name): return json.loads((root/name).read_text(encoding='utf-8'))
def write(name, value):
    data = (json.dumps(value, ensure_ascii=False, indent=2)+'\n').encode('utf-8')
    (root/name).write_bytes(data)
    return hashlib.sha256(data).hexdigest().upper()
catalog = read('recipes-q04.json')
recipe = catalog['recipes'][0]
recipe.update(recipeId='R008-Q04-MANUAL', version='1.0.0-test', plcRecipeId=203)
recipe['fCode']['virtualExactPayload'] = 'TEST-TRAY-0203'
for position in recipe['positions']:
    if 'resolvedFlipPosition' in position:
        position['resolvedFlipPosition'].update(mode='manual', manualWaitMs=120000)
catalog['source']['manualBasis'] = 'Protocol 3.1.5(8); U04; Test/Q04/manual/1.0.0'
digest = write('recipes-q04-manual.json', catalog)
worker = read('worker-manifest-q04.json')
worker.update(id='q04-manual-worker', fCode='TEST-TRAY-0203')
write('worker-manifest-q04-manual.json', worker)
fixture = read('fixture-q04.json')
fixture.update(caseId='Q04-MANUAL', fCode='TEST-TRAY-0203')
fixture['recipeRef'].update(recipeId=recipe['recipeId'], version=recipe['version'], catalogDigest=digest)
fixture.update(recipeCatalogPath=str((root/'recipes-q04-manual.json').resolve()),
               workerManifestPath=str((root/'worker-manifest-q04-manual.json').resolve()))
write('fixture-q04-manual.json', fixture)
print(json.dumps(dict(caseId='Q04-MANUAL', catalogDigest=digest, manualWaitMs=120000)))

# E applies to the source BASE assembly, not the Q04 COMPRESSOR.
catalog = read('recipes-assembly-a-e.json')
recipe = catalog['recipes'][0]
recipe.update(recipeId='R008-ASSEMBLY-A-E-MANUAL', version='1.0.0-test', plcRecipeId=208)
recipe['fCode']['virtualExactPayload'] = 'TEST-TRAY-0208'
for position in recipe['positions']:
    position['resolvedFlipPosition'].update(mode='manual', manualWaitMs=120000)
catalog['source']['manualBasis'] = 'Protocol 3.1.5(8); U04; S3 A BASE E source retained'
digest = write('recipes-assembly-a-e-manual.json', catalog)
worker = read('worker-manifest-assembly-a-e.json')
worker.update(id='assembly-a-e-manual-worker', fCode='TEST-TRAY-0208')
write('worker-manifest-assembly-a-e-manual.json', worker)
fixture = read('fixture-assembly-a-e.json')
fixture.update(caseId='ASSEMBLY-A-E-MANUAL', fCode='TEST-TRAY-0208')
fixture['recipeRef'].update(recipeId=recipe['recipeId'], version=recipe['version'], catalogDigest=digest)
fixture.update(recipeCatalogPath=str((root/'recipes-assembly-a-e-manual.json').resolve()),
               workerManifestPath=str((root/'worker-manifest-assembly-a-e-manual.json').resolve()))
write('fixture-assembly-a-e-manual.json', fixture)
print(json.dumps(dict(caseId='ASSEMBLY-A-E-MANUAL', catalogDigest=digest, manualWaitMs=120000)))
