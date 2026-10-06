"""Finite actual 016 migration review, separate from FixtureOnly admission self-test."""
import collections, hashlib, json, sys
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'scripts/workflow'))
import migration_audit

def audit(folder):
    folder = Path(folder)
    registration = json.loads((ROOT / 'specs/016-public-preparation-tray-check-unload/migration-affected-map.json').read_text(encoding='utf-8-sig'))
    original = json.loads((ROOT / 'backend/tests/Gaode.Rules.Tests/Architecture/009-test-obligations.json').read_text(encoding='utf-8-sig'))
    errors = []
    if original['initialRegistrationIntegrity'] != registration['initialRegistrationIntegrity']:
        errors.append('OriginalRegistrationIntegrityChanged')
    initial = migration_audit.initial_registration(original['groups'])
    integrity = hashlib.sha256(json.dumps(initial, sort_keys=True, ensure_ascii=False).encode()).hexdigest()
    if integrity != original['initialRegistrationIntegrity']:
        errors.append('OriginalObligationsChanged')
    if ([g['migrationId'] for g in original['groups']] != [f'T{i:02}' for i in range(1,54)]
            or sum(len(g['oldMethods']) for g in original['groups']) != 193
            or sum(len(m['dataRows']) for g in original['groups'] for m in g['oldMethods']) != 245):
        errors.append('OriginalMethodOrDataRowMissing')
    roots = [folder]
    references = json.loads((folder / 'gate-references.json').read_text(encoding='utf-8-sig'))
    roots.append(Path(references['boundary']))
    results = collections.defaultdict(list)
    for root in roots:
        for path in root.rglob('*.trx'):
            for row in ET.parse(path).findall('.//{*}UnitTestResult'):
                results[row.attrib['testName']].append({'evidence':str(path), 'outcome':row.attrib['outcome']})
    reviewed = []
    for obligation in registration['active']:
        evidence = []
        for name, expected in obligation.get('reviewedCurrentSources', {}).items():
            path = ROOT / name
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
                errors.append(obligation['originalObligation'] + ':ReviewedCurrentSourceChanged:' + name)
        for method in obligation.get('requiredMethods', []):
            found = [(name, rows) for name, rows in results.items() if name.split('(')[0].endswith('.' + method)]
            if not found: errors.append(obligation['originalObligation'] + ':RequiredMethodNotExecuted:' + method)
            for name, rows in found:
                if not any(r['outcome'] == 'Passed' for r in rows) or any(r['outcome'] != 'Passed' for r in rows):
                    errors.append(obligation['originalObligation'] + ':ExecutionNotPassed:' + name)
                evidence.append({'methodAndDataRow':name, 'actualExecution':rows})
        for name in obligation.get('requiredDisplayNames', []):
            rows = results.get(name, [])
            if len(rows) != 1 or rows[0]['outcome'] != 'Passed':
                errors.append(obligation['originalObligation'] + ':MissingDuplicateOrNotPassed:' + name)
            evidence.append({'methodAndDataRow':name, 'actualExecution':rows})
        reviewed.append({**obligation, 'actualEvidence':evidence, 'completed':bool(evidence) and not any(e.startswith(obligation['originalObligation'] + ':') for e in errors)})
    result = {'result':'Rejected' if errors else 'PassedForDeclaredActive016Scope',
              'scope':registration['scope'], 'fixtureOnly':False, 'originalRegistrationRetained':True,
              'activeObligations':reviewed, 'independentRetainedHistory':registration['independentRetainedHistory'],
              'fullHistoricalMigrationAdmission':'NotClaimed', 'errors':errors}
    (folder / 'migration-affected-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return result

if __name__ == '__main__':
    value = audit(sys.argv[1]); print(value['result']); sys.exit(1 if value['errors'] else 0)
