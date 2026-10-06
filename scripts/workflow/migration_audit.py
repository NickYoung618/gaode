"""009's finite, registered migration obligations; never discovers new expectations.

Source correspondence and execution admission are separate. A source match is
not execution evidence, and local components do not discharge process duties.
"""
import hashlib
import json
import re
from pathlib import Path


def audit010(root, registry, manifest=None, observed=None):
    """Finite 010 source/atomic-row correspondence. Never infer passed execution.

    The reviewed original snapshots are immutable. A retained method still needs
    current case evidence at final closure; a removed method needs its replacement.
    """
    root = Path(root)
    errors, required = [], set()
    methods = registry.get('methods', [])
    original = [m['original'] for m in methods]
    integrity = hashlib.sha256(json.dumps(original, ensure_ascii=False, sort_keys=True).encode()).hexdigest()
    if len(methods) != 115 or sum(len(m['dataRows']) for m in original) != 155 or integrity != registry.get('originalIntegrity'):
        errors.append('Original010ObligationsChanged')
    cases = {c['caseId']: c for c in (manifest or {}).get('cases', [])}
    for method in methods:
        targets = method.get('currentMappings', [])
        if not targets or not method.get('decision') or not method.get('independentBasis'):
            errors.append(method['id'] + ':MissingReview')
        if [r['originalDataRow'] for r in method.get('rowCoverage', [])] != method['original']['dataRows']:
            errors.append(method['id'] + ':OriginalDataRowUnmapped')
        if [(a['originalLine'], a['originalText']) for a in method.get('assertionCoverage', [])] != [
                (a['line'], a['text']) for a in method['original']['assertions']]:
            errors.append(method['id'] + ':OriginalAssertionUnmapped')
        ids = {t['caseId'] for t in targets}
        for row in method.get('rowCoverage', []):
            if not row['requiredCaseIds'] or not set(row['requiredCaseIds']).issubset(ids):
                errors.append(method['id'] + ':UnreviewedDataRowTarget')
        for target in targets:
            path = root / target['path']
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != target['reviewedSourceSha256']:
                errors.append(method['id'] + ':ReviewedSourceChanged:' + target['path'])
            elif not re.search(r'\b' + re.escape(target['method'].split('.')[-1]) + r'\s*\(', path.read_text(encoding='utf-8-sig')):
                errors.append(method['id'] + ':TargetMethodMissing')
            required.add(target['caseId'])
            if manifest is not None and any(cases.get(target['caseId'], {}).get(k) != target.get(k)
                    for k in ('method', 'dataRowId', 'dataRow', 'evidenceLevel', 'set')):
                errors.append(method['id'] + ':ManifestIdentityMismatch:' + target['caseId'])
    for group in ('sourceCoverage', 'newObligationCoverage'):
        for obligation in registry.get(group, []):
            if not obligation.get('caseIds'):
                errors.append(obligation['id'] + ':NoExecutableTarget')
            required.update(obligation.get('caseIds', []))
    if {d['id'] for d in registry.get('deletionReview', [])} != {f'D{i:02}' for i in range(1, 12)}:
        errors.append('DeletionReviewIncomplete')
    active = '\n'.join(p.read_text(encoding='utf-8-sig') for p in (root / 'backend/src').rglob('*.cs')
                       if not set(p.parts).intersection({'bin', 'obj'}))
    for item in registry.get('deletionReview', []):
        for symbol in item.get('absentActiveSymbols', []):
            if re.search(r'\b' + re.escape(symbol) + r'\b', active):
                errors.append(item['id'] + ':OldActiveSymbol:' + symbol)
        for path in item.get('paths', []):
            if not (root / path).exists(): errors.append(item['id'] + ':RetainedConsumerMissing:' + path)
    for path in registry.get('actualDeletedFiles', []):
        if (root / path).exists(): errors.append('ObsoleteFileStillExists:' + path)
    if manifest is not None:
        errors.extend('Unregistered010Case:' + c for c in sorted(required - cases.keys()))
    if observed is not None:
        # Ledger validity/identity is checked by the shared verifier before this
        # cross-reference step. Never accept a source-only migration as execution.
        done = {r['caseId'] for r in observed if r.get('executed') and r.get('discovered') and r.get('outcome') == 'Passed'}
        errors.extend('MigrationTargetNotExecuted:' + c for c in sorted(required - done))
    return dict(schemaVersion='010-migration-audit/1', result='Rejected' if errors else 'Passed',
                evidenceLevel='SourceReview' if observed is None else 'ValidatedLedgerCrossReference',
                methods=len(methods), originalDataRows=155, requiredCases=len(required), errors=errors)


def initial_registration(groups):
    return [dict(migrationId=g['migrationId'], methods=[{
        k: v for k, v in item.items() if k in (
            'path', 'name', 'originalFileSha256', 'source', 'dataRows',
            'assertionLines', 'plannedCaseIds', 'historicalSkip')
    } for item in g['oldMethods']], scripts=[dict(path=item['path'], sha256=item['sha256'],
        assertionSites=[{k: v for k, v in site.items() if k in ('line', 'text', 'plannedCaseId')}
                        for site in item['assertionSites']]) for item in g['scriptAssertions']])
        for g in groups]


def audit(root, registry, manifest, observed, context):
    root = Path(root).resolve()
    failures, obligations, pending = [], [], []
    groups = registry.get('groups', [])
    cases = {c['caseId']: c for c in manifest['cases']}
    by_id = {}
    for row in observed:
        by_id.setdefault(row['caseId'], []).append(row)

    def reject(identity, reason):
        failures.append(dict(obligation=identity, reason=reason))

    source_cache = {}

    def source(path, sha=None, snippet=None):
        full = (root / path).resolve()
        if full not in source_cache:
            if not full.is_relative_to(root) or not full.is_file():
                return False
            data = full.read_bytes()
            source_cache[full] = (hashlib.sha256(data).hexdigest(), data.decode('utf-8-sig'))
        digest, text = source_cache[full]
        return (sha is None or digest == sha) and (snippet is None or snippet in text)

    def executed(identity, required):
        if not required:
            reject(identity, 'NoExecutableReplacement')
            return
        for case_id in required:
            case = cases.get(case_id)
            if case is None:
                reject(identity, 'UnregisteredCase:' + case_id)
                continue
            if case['scope'] != 'verify':
                pending.append(case_id)
                continue
            rows = by_id.get(case_id, [])
            if len(rows) != 1:
                reject(identity, 'MissingOrDuplicateExecution:' + case_id)
                continue
            row = rows[0]
            if not row.get('discovered') or not row.get('executed') or row.get('outcome') != 'Passed':
                reject(identity, 'ExecutionNotPassed:' + case_id)
            if any(row.get(k) != context.get(k) for k in ('runId', 'sourceDigest', 'manifestDigest', 'buildDigest')):
                reject(identity, 'StaleExecution:' + case_id)
            if not set(case.get('requiredEvidence', [])).issubset(row.get('evidenceKinds', [])):
                reject(identity, 'ExecutionEvidenceMissing:' + case_id)

    if [g['migrationId'] for g in groups] != [f'T{i:02}' for i in range(1, 54)]:
        reject('Registry', 'InitialGroupsMissingOrReordered')
    original = initial_registration(groups)
    integrity = hashlib.sha256(json.dumps(original, sort_keys=True, ensure_ascii=False).encode()).hexdigest()
    if integrity != registry.get('initialRegistrationIntegrity'):
        reject('Registry', 'OriginalObligationsChanged')
    methods = sum(len(g['oldMethods']) for g in groups)
    data_rows = sum(len(m['dataRows']) for g in groups for m in g['oldMethods'])
    if (methods, data_rows) != (193, 245):
        reject('Registry', 'InitialMethodOrDataRowMissing')
    for group in groups:
        for method in group['oldMethods']:
            identity = group['migrationId'] + '/' + method['name']
            replacements = method.get('replacementMethods', [])
            if not replacements or not method.get('migrationReview'):
                reject(identity, 'ReplacementReviewMissing')
            executable = set()
            for replacement in replacements:
                if not source(replacement['path'], replacement.get('reviewedSourceSha256')):
                    reject(identity, 'ReviewedSourceChanged:' + replacement['path'])
                for assertion in replacement.get('assertionSources', []):
                    if not source(assertion['path'], assertion.get('sha256')):
                        reject(identity, 'AssertionSourceMissingOrChanged:' + assertion['path'])
                    for snippet in assertion.get('assertions', []):
                        if not source(assertion['path'], snippet=snippet):
                            reject(identity, 'ReviewedAssertionMissing:' + assertion['path'])
                for row in replacement.get('executableRows', []):
                    actual = cases.get(row['caseId'], {})
                    if any(actual.get(k) != row.get(k) for k in ('suite', 'method', 'dataRow')):
                        reject(identity, 'CaseIdentityChanged:' + row['caseId'])
                    executable.add(row['caseId'])
            coverage = method.get('rowCoverage', [])
            if [r.get('plannedCaseId') for r in coverage] != method['plannedCaseIds']:
                reject(identity, 'OriginalDataRowUnmapped')
            for old, mapped in zip(method['dataRows'], coverage):
                if mapped.get('oldRow') != old['row'] or mapped.get('oldLiteral') != old['literal']:
                    reject(identity, 'OriginalDataRowChanged')
                required = mapped.get('requiredCaseIds', [])
                if not set(required).issubset(executable):
                    reject(identity, 'UnreviewedReplacementCase')
                executed(mapped['plannedCaseId'], required)
                obligations.append(dict(id=mapped['plannedCaseId'], requiredCaseIds=required))
        for script in group['scriptAssertions']:
            identity = group['migrationId'] + '/' + script['path']
            for current in script.get('currentSources', []):
                if not source(current['path'], current['sha256']):
                    reject(identity, 'ReviewedScriptChanged:' + current['path'])
            for site in script['assertionSites']:
                if not site.get('replacementAssertions'):
                    reject(identity, 'OriginalScriptAssertionUnmapped')
                for replacement in site.get('replacementAssertions', []):
                    if not source(replacement['path'], snippet=replacement['snippet']):
                        reject(identity, 'ScriptAssertionMissing:' + replacement['path'])
            if script.get('originalEngineChecks', '').startswith('Pending'):
                reject(identity, 'OriginalWorkflowEngineChecksNotExecuted')
            required = script.get('requiredCaseIds', []) + script.get('requiredProcessCaseIds', [])
            executed(identity, required)
            obligations.append(dict(id=identity, requiredCaseIds=required))
    return dict(schemaVersion='009-migration-audit/1', scope='LocalMigrationObligations',
        result='Rejected' if failures else 'Passed', groups=len(groups), oldMethods=methods,
        oldDataRows=data_rows, originalRegistrationDigest=integrity, failures=failures,
        obligations=obligations, pendingProcessCases=sorted(set(pending)), overall009Passed=False, **context)
