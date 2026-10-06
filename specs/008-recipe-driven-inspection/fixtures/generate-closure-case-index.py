"""Append current C/operation fixture references without altering Q/history entries."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parent
definitions = [
    ('GROUP-F', 'group-f', ['C03'], 12, 6, 0, 2, 0),
    ('GROUP-F-MIXED', 'group-f-mixed', ['C03', 'C06'], 12, 6, 0, 2, 0),
    ('GROUP-A-E', 'group-a-e', ['C03'], 28, 14, 2, 6, 0),
    ('ASSEMBLY-A-E', 'assembly-a-e', ['C04'], 6, 3, 1, 1, 0),
    ('ASSEMBLY-A-E-NOCODE', 'assembly-a-e-no-code', ['C04'], 6, 3, 1, 1, 0),
    ('ASSEMBLY-A-E-ERROR', 'assembly-a-e-error', ['C04'], 6, 3, 1, 1, 0),
    ('ASSEMBLY-A-E-NG', 'assembly-a-e-ng', ['C04', 'C06'], 6, 3, 1, 1, 0),
    ('Q04-MANUAL', 'q04-manual', ['C02'], 4, 2, 0, 0, 1),
    ('ASSEMBLY-A-E-MANUAL', 'assembly-a-e-manual', ['C02', 'C04'], 6, 3, 1, 0, 1),
    ('ROT-PART-OK', 'rot-part-ok', ['C05'], 4, 2, 0, 0, 0),
    ('ROT-PART-NG', 'rot-part-ng', ['C05', 'C06'], 4, 2, 0, 0, 0),
    ('ROT-PART-PENDING', 'rot-part-pending', ['C05', 'C06'], 4, 2, 0, 0, 0),
    ('ROT-ASSEMBLY-OK', 'rot-assembly-ok', ['C05'], 6, 3, 1, 0, 0),
    ('RECOVERY-3D', 'recovery-3d', ['C07'], 2, 1, 0, 0, 0),
]
cases_file = root/'cases.json'
cases = json.loads(cases_file.read_text(encoding='utf-8-sig'))
operations = []
for case, slug, cids, captures, fusions, e_calls, flips, manual in definitions:
    path = root/f'fixture-{slug}.json'
    fixture = json.loads(path.read_text(encoding='utf-8-sig'))
    refs = {}
    for key in ['recipeCatalogPath', 'imageManifestPath', 'workerManifestPath', 'workerScriptPath']:
        source = Path(fixture[key])
        refs[key] = {'path': str(source.resolve()), 'sha256': hashlib.sha256(source.read_bytes()).hexdigest().upper()}
    if refs['recipeCatalogPath']['sha256'] != fixture['recipeRef']['catalogDigest']:
        raise ValueError(f'{case}: stale catalog reference')
    operations.append(dict(caseId=case, covers=cids, fixture=path.name, recipeRef=fixture['recipeRef'],
        scenarioId=fixture['scenarioId'], occupiedSlots=fixture['occupiedSlots'], fCode=fixture['fCode'],
        purpose='Test', mediaSource='Simulated/Test', productionAvailable=False, references=refs,
        expected=dict(initial3d=1, initialF=1, detectionCaptures=captures, fusions=fusions,
                      detectionCalls=captures+fusions, eCalls=e_calls, automaticFlips=flips,
                      manualFlips=manual, specialActions=4 if slug.startswith('rot-') else 0,
                      postFlip3d=0),
        businessBasis='business-decisions-20260926 U01-U08; source model rows; existing virtual range',
        pageStatus='NotRun', evidenceIsConfigurationOnly=True))
cases['operationCases'] = operations
cases['cCoverageConfiguration'] = {
    'C01': ['Q01-PARAM', 'Q02'], 'C02': ['Q04-MANUAL', 'ASSEMBLY-A-E-MANUAL'],
    'C03': ['GROUP-F', 'GROUP-F-MIXED', 'GROUP-A-E'],
    'C04': ['ASSEMBLY-A-E', 'ASSEMBLY-A-E-NOCODE', 'ASSEMBLY-A-E-ERROR', 'ASSEMBLY-A-E-NG'],
    'C05': ['ROT-PART-OK', 'ROT-PART-NG', 'ROT-PART-PENDING', 'ROT-ASSEMBLY-OK'],
    'C06': ['Q03-NG', 'Q03-Pending', 'GROUP-F-MIXED', 'ASSEMBLY-A-E-NG'],
    'C07': ['RECOVERY-3D'], 'C08': ['Q01', 'Q01-PARAM']
}
cases_file.write_text(json.dumps(cases, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(dict(ordinaryQ=len([x for x in cases['cases'] if x['caseId'][1:].isdigit()]),
                     operationCases=len(operations), cConfigurations=8, pagePassedAdded=0)))
