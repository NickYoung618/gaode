"""Prepare one independent Q02 P03 sorting representative from approved Test inputs."""
import hashlib
import json
import copy
from pathlib import Path

repo = Path(__file__).resolve().parent.parent
fixtures = repo / 'specs/008-recipe-driven-inspection/fixtures'
base = fixtures / 'night-closure-20260926'
target = fixtures / 'disposition-p03-1.1.4'
if target.exists():
    raise SystemExit('Existing versioned fixture retained; choose a reviewed new version instead')
load = lambda name: json.loads((base / name).read_text(encoding='utf-8-sig'))
catalog = load('recipes-q02-pending.json')
worker = load('worker-manifest-q02-pending.json')
fixture = load('fixture-q02-pending.json')
catalog['recipes'][0]['version'] = '1.1.4-test-p03'
positions = catalog['recipes'][0]['positions']
source = next(p for p in positions if p['slotId'] == 'P03')
approved = next(p for p in positions if p['slotId'] == 'P01')['resolvedSortingTargets']['Pending']
source['resolvedSortingTargets'] = {'Pending': copy.deepcopy(approved)}
source['resolvedSortingTargets']['Pending']['testSourceRef'] = 'Test/VirtualSameTray/Pending/P03/1.1.4'
catalog['source']['basis'] += '; P03 Pending representative, original positions/actions unchanged'
worker['id'] = 'q02-pending-p03-worker'
worker['version'] = '1.0.2-test-p03'
worker['dispositionByTarget'][0]['objectIdSuffix'] = 'P:P03:M01'
fixture['caseId'] = 'Q02-PENDING-P03'
fixture['recipeRef']['version'] = catalog['recipes'][0]['version']
target.mkdir()
def save(name, value):
    p = target / name
    p.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return p
catalog_path = save('recipes-q02-pending-p03.json', catalog)
worker_path = save('worker-manifest-q02-pending-p03.json', worker)
fixture['recipeCatalogPath'] = str(catalog_path)
fixture['workerManifestPath'] = str(worker_path)
fixture['recipeRef']['catalogDigest'] = hashlib.sha256(catalog_path.read_bytes()).hexdigest().upper()
save('fixture-q02-pending-p03.json', fixture)
save('generation.json', {'sourceFiles': [{'path': str(base / n),
    'sha256': hashlib.sha256((base / n).read_bytes()).hexdigest().upper()} for n in
    ('recipes-q02-pending.json', 'worker-manifest-q02-pending.json', 'fixture-q02-pending.json')],
    'purpose': 'Test only; physical source P03 and existing target P15; no production mapping',
    'businessRunProduced': False})
print(fixture['caseId'], fixture['recipeRef']['catalogDigest'])
