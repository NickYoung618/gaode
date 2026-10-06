"""Finite recipe isolation checks and the 010 verification profile.

This module shares the existing workflow runner, reports and workspace lock.
Direct test/debug commands do not constitute project acceptance.
"""
from __future__ import annotations

import ast
import hashlib
import importlib.util
import json
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CHECKER_VERSION = '010-recipe-boundary/1'


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    # Evidence is immutable per attempt; an existing report must never be overwritten.
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


@lru_cache(maxsize=1)
def script_parser():
    spec = importlib.util.spec_from_file_location('boundary009', ROOT / 'scripts/check-009-script-boundary.py')
    boundary = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(boundary)
    return boundary


def check_script(item, parsed=None):
    """Check script actions, not labels or occurrences of the word Test.

    The 009 parser provides syntax/closure/protocol protection for all supported
    script languages. This additional finite Python action rule rejects direct
    workflow continuation and completion writes; normal API driving is allowed.
    """
    boundary = script_parser()
    language = boundary.language(item['path'])
    if language is None:
        return dict(parsed=False, violations=[], errors=['UnsupportedScriptLanguage'])
    result = parsed if parsed is not None else boundary.parse_batch(language, [item])[0]
    forbidden_calls = {'ExecutePostFlipComponentAsync', 'RescanWholeTrayAsync',
                       'RunNextStageAsync', 'CompleteWorkflowAsync',
                       'SetCompleted', 'SetStageCompleted', 'Complete-Workflow', 'Start-NextStage'}
    if result.get('parsed') and language == 'powershell':
        if 'workflowActions' not in result:
            result['errors'].append(dict(code='B05', message='InvocationFactsMissing'))
        for action in result.get('workflowActions', []):
            if action['name'] in forbidden_calls:
                result['violations'].append(dict(ruleId='B02', path=item['path'], line=action['line'],
                    column=action['column'], message='Script directly schedules a business stage or completion.'))
    if not result.get('parsed') or language != 'python':
        return result
    tree = ast.parse(item['source'], filename=item['path'])
    violations = result['violations']
    for node in ast.walk(tree):
        if not isinstance(node, ast.Call):
            continue
        name = node.func.attr if isinstance(node.func, ast.Attribute) else node.func.id if isinstance(node.func, ast.Name) else ''
        reason = None
        if name in forbidden_calls:
            reason = 'Script directly schedules a business stage or completion.'
        if name.lower() in {'execute', 'executemany', 'executescript'}:
            for argument in node.args:
                if isinstance(argument, ast.Constant) and isinstance(argument.value, str):
                    sql = ' '.join(argument.value.casefold().split())
                    if ('update runs' in sql or 'insert into stageevents' in sql) and any(
                            state in sql for state in ('completed', 'awaitingmanualremoval', 'readyforunlock')):
                        reason = 'Script writes workflow success instead of invoking the formal operation.'
        if reason:
            violations.append(dict(ruleId='B02', path=item['path'], line=node.lineno,
                                   column=node.col_offset + 1, message=reason))
    return result


def check_script_cases(path=None):
    manifest = read_json(path or ROOT / 'scripts/architecture/010-script-boundary-cases.json')
    outcomes = []
    for case in manifest['cases']:
        result = check_script(case)
        required = case.get('rejectRule')
        accepted = bool(result.get('parsed')) and not result.get('errors')
        passed = accepted and (any(v['ruleId'] == required for v in result['violations'])
                               if required else not result['violations'])
        outcomes.append(dict(caseId=case['caseId'], outcome='Passed' if passed else 'Failed',
                             observation=result))
    return outcomes


def check_repository_scripts():
    parser = script_parser()
    inventory = read_json(ROOT / 'backend/tests/Gaode.Rules.Tests/Architecture/009-boundary-inventory.json')
    registered = {item['path'] for item in inventory['files']}
    discovered = set()
    for base in inventory['discoveryRoots']:
        discovered.update(p.relative_to(ROOT).as_posix() for p in (ROOT / base).rglob('*')
            if p.is_file() and parser.language(p) and not set(p.parts).intersection(inventory['excludedGeneratedSegments']))
    discovered.update(p.name for p in ROOT.iterdir() if p.is_file() and parser.language(p))
    errors = parser.inventory_errors(inventory['files'], discovered, inventory['communicationProbePaths'])
    files, violations = [], []
    for language in ('python', 'powershell', 'javascript'):
        items = []
        for entry in inventory['files']:
            if parser.language(entry['path']) != language:
                continue
            path = ROOT / entry['path']
            if not path.is_file():
                errors.append(dict(code='B05', path=entry['path'], message='RegisteredScriptMissing'))
                continue
            items.append(dict(path=entry['path'], role=entry['role'], source=path.read_text(encoding='utf-8-sig')))
        for item, parsed in zip(items, parser.parse_batch(language, items), strict=True):
            result = check_script(item, parsed)
            files.append(dict(path=item['path'], parsed=result['parsed'], sha256=sha(ROOT / item['path'])))
            violations.extend(result['violations'])
            errors.extend(result['errors'])
            errors.extend(parser.dependency_errors(item, result['dependencies'], registered))
            version = parser.parser_error(language, result.get('parserVersion'), inventory['scriptParserVersions'][language])
            if version:
                errors.append(version)
    cases = check_script_cases()
    return dict(schemaVersion='010-script-boundary/1', checker=CHECKER_VERSION, files=files,
                errors=errors, violations=violations, cases=cases,
                result='Rejected' if errors or violations or any(c['outcome'] != 'Passed' for c in cases) else 'Passed')
# Shared current-attempt evidence validation. Every acceptance consumer calls this
# code; this is a finite addition to the existing runner, not a new test platform.
from datetime import datetime, timezone
import os
import sys
import uuid
import xml.etree.ElementTree as ET

IDENTITY = ('verificationAttemptId', 'parentAttemptId', 'profile', 'sourceDigest',
            'buildDigest', 'manifestDigest', 'inputDigest', 'checkerDigest')
LIGHT = 'scripts/workflow/010-lightweight-cases.json'
REQUIRED = 'scripts/workflow/010-required-cases.json'
EXCLUDED = {'bin', 'obj', '.git', 'node_modules', 'dist', '__pycache__', 'TestResults'}
SOURCE_SUFFIXES = {'.cs', '.csproj', '.props', '.targets', '.json', '.py', '.ps1', '.psm1',
                   '.md', '.yml', '.yaml', '.js', '.cjs', '.mjs', '.ts', '.csv', '.png', '.bmp', '.jpg'}
CHECKER_FILES = ['scripts/workflow/recipe_execution_010.py', 'scripts/workflow/migration_audit.py',
                 'backend/tests/Gaode.Rules.Tests/Architecture/RecipeExecutionBoundaryChecker.cs',
                 'backend/tests/Gaode.Rules.Tests/Architecture/ProtocolBoundaryChecker.cs']


def now():
    return datetime.now(timezone.utc).isoformat()


def value_digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(',', ':')).encode()).hexdigest()


def source_files():
    # Superset of the independently re-enumerated Roslyn closure, so moving a
    # business helper or changing its Role cannot remove it from the freeze.
    files = {}
    for base in ('backend', 'VirtualPlc', 'scripts', 'workflows', 'specs', 'frontend'):
        for path in (ROOT / base).rglob('*'):
            if (path.is_file() and not EXCLUDED.intersection(path.relative_to(ROOT).parts)
                    and path.suffix.lower() in SOURCE_SUFFIXES and path.name != 'tasks.md'
                    and 'checklists' not in path.parts):
                files[path.relative_to(ROOT).as_posix()] = sha(path)
    for name in ('global.json', 'Directory.Build.props', 'Directory.Packages.props'):
        if (ROOT / name).is_file(): files[name] = sha(ROOT / name)
    if not files: raise ValueError('SourceEnumerationEmpty')
    return dict(sorted(files.items()))


def input_digest(files):
    return value_digest({p: h for p, h in files.items() if '/fixtures/' in p.lower() or '/examples/' in p.lower()
                         or p.endswith(('.csv', '.png', '.bmp', '.jpg'))})


def build_files(light=True):
    entries = ['backend/tests/Gaode.Rules.Tests/bin/Debug/net10.0/Gaode.Rules.Tests.dll']
    if not light:
        import runner
        entries = runner.required_build_binaries()
    files = {}
    for name in entries:
        binary = ROOT / name
        if not binary.is_file(): raise ValueError('BuildOutputMissing:' + name)
        for p in binary.parent.rglob('*'):
            if p.is_file() and p.suffix.lower() in ('.dll', '.json') and not EXCLUDED.intersection(p.relative_to(binary.parent).parts):
                files[p.relative_to(ROOT).as_posix()] = sha(p)
    return dict(sorted(files.items()))


def manifest(path):
    value = read_json(path)
    if value.get('schemaVersion') not in ('010-lightweight-cases/1', '010-required-cases/1'):
        raise ValueError('ManifestSchemaInvalid')
    ids = [c['caseId'] for c in value.get('cases', [])]
    if not ids or len(ids) != len(set(ids)): raise ValueError('RequiredCaseMissingOrDuplicate')
    for name in value.get('requiredFiles', []):
        if not (ROOT / name).is_file(): raise ValueError('RequiredScannerOrInputMissing:' + name)
    if not value.get('requiredFiles'): raise ValueError('RequiredFilesMissing')
    return value


def make_context(parent, profile, manifest_path, light=True):
    files = source_files()
    binaries = build_files(light)
    checker = {p: sha(ROOT / p) for p in CHECKER_FILES}
    return dict(verificationAttemptId=uuid.uuid4().hex, parentAttemptId=parent, profile=profile,
                startedAt=now(), sourceDigest=value_digest(files), sourceFiles=files,
                buildDigest=value_digest(binaries), buildFiles=binaries,
                manifestPath=str(Path(manifest_path).resolve()), manifestDigest=sha(manifest_path),
                inputDigest=input_digest(files), checkerDigest=value_digest(checker), checkerFiles=checker,
                light=light)


def identity(context):
    return {k: context[k] for k in IDENTITY}


def instant(value):
    return datetime.fromisoformat(value.replace('Z', '+00:00')).astimezone(timezone.utc)


def validate_rows(expected, rows, context, errors=()):
    """Pure production ledger check used for all G/C data samples and real runs."""
    failures = list(errors)
    wanted = {c['caseId']: c for c in expected}
    if len(wanted) != len(expected) or not wanted: failures.append('ExpectedCaseMissingOrDuplicate')
    by_id = {}
    for row in rows:
        by_id.setdefault(row.get('caseId'), []).append(row)
        if row.get('parseError') or row.get('outcome') in ('Error', 'ParseError'):
            failures.append('AnyReportParseFailed:' + str(row.get('caseId')))
        if row.get('caseId') not in wanted: failures.append('UnexpectedCase:' + str(row.get('caseId')))
    for cid, case in wanted.items():
        found = by_id.get(cid, [])
        if len(found) != 1:
            failures.append('MissingOrDuplicateExecution:' + cid); continue
        row = found[0]
        if not row.get('discovered') or not row.get('executed') or row.get('outcome') != 'Passed':
            failures.append('NotDiscoveredExecutedPassed:' + cid)
        if any(row.get(k) != context.get(k) for k in IDENTITY): failures.append('StaleIdentity:' + cid)
        if any(row.get(k) != case.get(k) for k in ('method', 'dataRowId', 'dataRow')):
            failures.append('CaseDataIdentityMismatch:' + cid)
        if not set(case.get('requiredEvidence', [])).issubset(row.get('evidenceKinds', [])):
            failures.append('RequiredEvidenceMissing:' + cid)
        try:
            if instant(row['startedAt']) < instant(context['startedAt']) or instant(row['endedAt']) < instant(row['startedAt']):
                failures.append('OldOrInvalidTime:' + cid)
        except (KeyError, TypeError, ValueError): failures.append('TimeParseFailed:' + cid)
    return dict(result='Rejected' if failures else 'Passed', requiredCount=len(wanted),
                observedCount=len(rows), errors=failures)


def row(case, context, outcome, start, end, discovered=True, executed=True, **extra):
    return dict(identity(context), caseId=case['caseId'], method=case.get('method'),
                dataRowId=case.get('dataRowId'), dataRow=case.get('dataRow'), discovered=discovered,
                executed=executed, outcome=outcome, startedAt=start, endedAt=end,
                evidenceKinds=case['requiredEvidence'], **extra)


def trx_rows(path, discovery, expected, context):
    tree = ET.parse(path)
    errors, rows = [], []
    counters = tree.find('.//{*}Counters')
    tests = tree.findall('.//{*}UnitTestResult')
    if counters is None or int(counters.get('total', '0')) != len(tests) or not tests:
        errors.append('TrxCountersMissingOrInconsistent')
    for field in ('notExecuted', 'failed', 'error', 'timeout', 'aborted', 'inconclusive'):
        if counters is not None and int(counters.get(field, '0')): errors.append('TrxNonPassing:' + field)
    names = [t.get('testName') for t in tests]
    if len(names) != len(set(names)): errors.append('DuplicateTrxIdentity')
    discovery_text = Path(discovery).read_text(encoding='utf-8-sig')
    expected_names = set()
    for case in expected:
        name = case['method'] + ('(' + case['dataRow'] + ')' if case.get('dataRow') else '')
        expected_names.add(name)
        selected = [t for t in tests if t.get('testName') == name]
        if len(selected) != 1: errors.append('TrxDataRowMissingOrDuplicate:' + name); continue
        test = selected[0]
        rows.append(row(case, context, test.get('outcome'), test.get('startTime'), test.get('endTime'),
                        discovered=name in {line.strip() for line in discovery_text.splitlines()},
                        executed=test.get('outcome') not in (None, 'NotExecuted', 'Skipped'),
                        executionId=test.get('executionId'), testId=test.get('testId'), report=str(path)))
    if set(names) - expected_names: errors.append('UnexpectedTrxRows:' + ','.join(sorted(set(names) - expected_names)))
    return rows, errors


def validate_reference(proof, original_context, current_context, original_case, original_row):
    # Conservative supported reuse: the complete dependency snapshot is equal.
    # No selective directory/Role declaration can omit changed business code.
    if (original_case.get('set') != 'B' or original_case.get('evidenceLevel') == 'FullRun'
            or not proof.get('independentImpactReview') or proof.get('affected') is not False
            or proof.get('originalAttempt') != original_context['verificationAttemptId']
            or proof.get('caseId') != original_case['caseId']
            or proof.get('sourceFiles') != original_context.get('sourceFiles')
            or original_context.get('sourceFiles') != current_context.get('sourceFiles')
            or any(original_context.get(k) != current_context.get(k) for k in
                   ('sourceDigest', 'buildDigest', 'inputDigest', 'manifestDigest', 'checkerDigest'))):
        return False
    return validate_rows([original_case], [original_row], original_context)['result'] == 'Passed'


def validate_bundle(path, parent, profile, *, current=True):
    """Reparse native reports at each final rejection point; ignore passed booleans."""
    try:
        folder = Path(path)
        context = read_json(folder / 'context.json')
        document = read_json(folder / 'bundle.json')
        if context['parentAttemptId'] != parent or context['profile'] != profile: raise ValueError('OldParentOrProfile')
        actual_manifest = manifest(context['manifestPath'])
        if sha(context['manifestPath']) != context['manifestDigest']: raise ValueError('OldManifest')
        if document['contextDigest'] != value_digest(context): raise ValueError('ContextChanged')
        for name, digest in document['reports'].items():
            target = (folder / name).resolve()
            if not target.is_relative_to(folder.resolve()) or not target.is_file() or sha(target) != digest:
                raise ValueError('MissingOrChangedRawReport:' + name)
        if current and (source_files() != context['sourceFiles'] or build_files(context['light']) != context['buildFiles']):
            raise ValueError('SourceOrBuildChanged')
        if value_digest(context['sourceFiles']) != context['sourceDigest'] or value_digest(context['buildFiles']) != context['buildDigest']:
            raise ValueError('SnapshotDigestInvalid')
        if input_digest(context['sourceFiles']) != context['inputDigest'] or value_digest(context['checkerFiles']) != context['checkerDigest']:
            raise ValueError('InputOrCheckerDigestInvalid')
        expected = [c for c in actual_manifest['cases'] if c['caseId'] in document['caseIds']]
        if len(expected) != len(document['caseIds']): raise ValueError('RequiredCaseMissing')
        if context['light'] and {c['caseId'] for c in expected} != {c['caseId'] for c in actual_manifest['cases']}:
            raise ValueError('LightweightFilterForbidden')
        rows, errors = [], []
        for command in document['commands']:
            if any(command[k] not in document['reports'] for k in ('report', 'discovery', 'log') if command.get(k)):
                errors.append('NativeReportNotBoundToCredential')
            if command['exitCode'] != 0: errors.append('CommandFailed:' + command['kind'])
            if command['context'] != identity(context): errors.append('CommandIdentityMismatch')
            cases = [c for c in expected if c['caseId'] in command['caseIds']]
            if any(c['kind'] != command['kind'] for c in cases): errors.append('WrongExecutionKind')
            if command['kind'] == 'dotnet':
                found, parse_errors = trx_rows(folder / command['report'], folder / command['discovery'], cases, context)
                rows += found; errors += parse_errors
            elif command['kind'] == 'scripts':
                report = read_json(folder / command['report'])
                if report['result'] != 'Passed' or report['errors'] or report['violations'] or not report['files']:
                    errors.append('ScriptSourceRejectedOrMissing')
                outcomes = {'SCRIPT-Repository': report['result']} | {c['caseId']: c['outcome'] for c in report['cases']}
                for c in cases: rows.append(row(c, context, outcomes.get(c['caseId'], 'Missing'), command['startedAt'], command['endedAt']))
            elif command['kind'] == 'selfcheck':
                report = read_json(folder / command['report'])
                if report['context'] != identity(context): errors.append('SelfcheckIdentityMismatch')
                indexed = {c['caseId']: c for c in report['cases']}
                if len(indexed) != len(report['cases']) or set(indexed) != {c['caseId'] for c in cases}:
                    errors.append('SelfcheckRowsMissingDuplicateOrExtra')
                for c in cases: rows.append(row(c, context, indexed.get(c['caseId'], {}).get('outcome', 'Missing'), command['startedAt'], command['endedAt']))
            elif command['kind'] in ('build', 'worker-selfcheck'):
                if command['kind'] == 'worker-selfcheck':
                    text = (folder / command['report']).read_text(encoding='utf-8-sig')
                    if 'Ran 2 tests' not in text or '\nOK' not in text: errors.append('WorkerSelfcheckIncomplete')
                for c in cases: rows.append(row(c, context, 'Passed' if command['exitCode'] == 0 else 'Failed', command['startedAt'], command['endedAt']))
            else: errors.append('UnrecognizedExecutionKind')
        if context['light']:
            if len([c for c in document['commands'] if c['kind'] == 'build']) != 1:
                errors.append('LightweightBuildInvocationMissing')
            source = read_json(folder / 'native/recipe-boundary.json')
            if (source.get('runId') != context['verificationAttemptId'] or source.get('result') != 'Passed'
                    or not source.get('files') or not source.get('closure') or source.get('violations')):
                errors.append('CurrentBusinessSourceProofInvalid')
            if not set(source.get('files', [])).issubset(context['sourceFiles']):
                errors.append('CompiledSourceMissingFromSnapshot')
        assessed = validate_rows(expected, rows, context, errors)
        return dict(assessed, context=context, rows=rows, path=str(folder), contextDigest=value_digest(context))
    except (OSError, ValueError, KeyError, TypeError, ET.ParseError) as error:
        return dict(result='Rejected', errors=[type(error).__name__ + ':' + str(error)], rows=[])


def final_gate(own_passed, credential, parent, profile, point):
    # point is recorded for diagnostics; it never changes the validation policy.
    checked = validate_bundle(credential, parent, profile) if credential else dict(result='Rejected', errors=['LightweightCredentialMissing'])
    if checked['result'] == 'Passed' and (not checked['context'].get('light') or
            Path(checked['context']['manifestPath']).resolve() != (ROOT / LIGHT).resolve()):
        checked = dict(result='Rejected', errors=['LightweightCredentialRequired'])
    return dict(passed=own_passed is True and checked['result'] == 'Passed', finalPoint=point,
                lightweight=checked, credential=str(credential) if credential else None,
                parentAttemptId=parent, profile=profile)


def test_command(cases):
    methods = sorted({c['method'] for c in cases})
    # Internal exact selection from the reviewed manifest, never caller filters.
    return '|'.join('FullyQualifiedName=' + name for name in methods)


def execute_dotnet(folder, context, cases, env, label, timeout=10800):
    import runner
    suite = cases[0]['suite']
    base = ['dotnet', 'test', f'backend/tests/{suite}/{suite}.csproj', '--no-build', '--no-restore',
            '--filter', test_command(cases)]
    discovery = label + '.discovery.log'; log = label + '.log'; report = label + '.trx'
    start = now()
    discover_code = runner.run_logged(base + ['--list-tests'], folder / discovery, env, timeout=300)
    code = runner.run_logged(base + ['--logger', 'trx;LogFileName=' + report,
        '--results-directory', str(folder), '--blame-hang-timeout', '15min', '--blame-hang-dump-type', 'none'],
        folder / log, env, timeout=timeout) if discover_code == 0 else discover_code
    return dict(kind='dotnet', context=identity(context), caseIds=[c['caseId'] for c in cases],
                command=base, startedAt=start, endedAt=now(), exitCode=code or discover_code,
                report=report, discovery=discovery, log=log)


def seal_bundle(folder, context, commands, case_ids):
    # Files are immutable within the new attempt folder. Child stores/process
    # evidence remain attached; only the finite native report set is reparsed.
    names = set()
    for command in commands:
        names.update(command[k] for k in ('report', 'discovery', 'log') if command.get(k))
    if context['light']: names.add('native/recipe-boundary.json')
    reports = {name: sha(folder / name) for name in sorted(names) if (folder / name).is_file()}
    write_json(folder / 'bundle.json', dict(schemaVersion='010-execution-bundle/1', contextDigest=value_digest(context),
               commands=commands, caseIds=case_ids, reports=reports, endedAt=now()))
    result = validate_bundle(folder, context['parentAttemptId'], context['profile'])
    write_json(folder / 'ledger.json', result)
    return result


def run_lightweight(parent, profile):
    import runner
    folder = ROOT / 'artifacts/recipe-execution-010' / parent / 'L'
    folder.mkdir(parents=True, exist_ok=False)
    fixed = manifest(ROOT / LIGHT)
    before = source_files()
    env = dict(os.environ, PYTHONUTF8='1', PYTHONDONTWRITEBYTECODE='1', DOTNET_NOLOGO='1')
    start = now()
    command = ['dotnet', 'build', 'backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj', '--no-restore']
    code = runner.run_logged(command, folder / 'build.log', env, timeout=900)
    if code: raise ValueError('LightweightBuildFailed:' + str(folder))
    context = make_context(parent, profile, ROOT / LIGHT)
    if before != context['sourceFiles']: raise ValueError('SourceChangedDuringLightweightBuild')
    write_json(folder / 'context.json', context)
    write_json(folder / 'fixed-cases.json', fixed)
    env.update(GAODE_010_ATTEMPT=context['verificationAttemptId'], GAODE_010_EVIDENCE_ROOT=str(folder / 'native'),
               GAODE_009_EVIDENCE_ROOT=str(folder / 'native'))
    commands = [dict(kind='build', context=identity(context), caseIds=[], command=command,
                     startedAt=start, endedAt=now(), exitCode=code, report='build.log', log='build.log')]
    commands.append(execute_dotnet(folder, context, [c for c in fixed['cases'] if c['kind'] == 'dotnet'], env, 'boundaries', 900))
    start = now()
    report = check_repository_scripts()
    write_json(folder / 'scripts.json', report)
    commands.append(dict(kind='scripts', context=identity(context), caseIds=[c['caseId'] for c in fixed['cases'] if c['kind']=='scripts'],
                         startedAt=start, endedAt=now(), exitCode=0 if report['result']=='Passed' else 1, report='scripts.json'))
    start = now()
    code = runner.run_logged([sys.executable, '-B', 'scripts/workflow/test_recipe_execution_010.py', '--selfcheck',
        '--context', str(folder / 'context.json'), '--output', str(folder / 'selfcheck.json')], folder / 'selfcheck.log', env, timeout=300)
    commands.append(dict(kind='selfcheck', context=identity(context), caseIds=[c['caseId'] for c in fixed['cases'] if c['kind']=='selfcheck'],
                         startedAt=start, endedAt=now(), exitCode=code, report='selfcheck.json', log='selfcheck.log'))
    seal_bundle(folder, context, commands, [c['caseId'] for c in fixed['cases']])
    return str(folder)


def begin_acceptance(parent, profile, delegated=None):
    """Every profile enters L. Delegation only accepts the same parent/profile."""
    if delegated:
        checked = final_gate(True, delegated, parent, profile, 'delegate')
        if not checked['passed']: raise ValueError('DelegatedLightweightCredentialRejected')
        return str(delegated)
    return run_lightweight(parent, profile)


def run_with_lightweight(parent, profile, work, *, point='_run_verify', delegated=None):
    credential = begin_acceptance(parent, profile, delegated)
    initial = final_gate(True, credential, parent, profile, point + ':admission')
    if not initial['passed']: return dict(initial, ownResult=None)
    own = work(credential)
    result = final_gate(own.get('passed') is True, credential, parent, profile, point)
    return dict(result, ownResult=own)


REGISTRY = 'backend/tests/Gaode.Rules.Tests/Architecture/010-test-obligations.json'
FIXTURES = 'backend/tests/Gaode.Integration.Tests/Fixtures/RecipeExecution010'


def review_prepared_inputs(fixed):
    import migration_audit
    review = migration_audit.audit010(ROOT, read_json(ROOT / REGISTRY), fixed)
    if review['result'] != 'Passed': raise ValueError('MigrationPreparationRejected:' + str(review['errors']))
    coverage = read_json(ROOT / FIXTURES / 'replacement-coverage.json')
    if {c['boundary'] for c in coverage['equivalent']} != {
            'media-content', 'algorithm-implementation', 'coordinate-provider',
            'catalog-and-code-decoding', 'formal-bindings'}:
        raise ValueError('ReplacementCoverageIncomplete')
    inputs = [coverage['independentExpected'], *coverage['sharedS']['inputs']]
    for boundary in coverage['equivalent']:
        inputs += boundary['baseline'] + boundary['replacement']
    for item in inputs:
        if sha(ROOT / item['path']).lower() != item['sha256'].lower():
            raise ValueError('PreparedInputChanged:' + item['path'])
    if {c['set'] for c in fixed['cases']} != {'B', 'E', 'S'}:
        raise ValueError('RequiredPhaseMissing')
    if [c['caseId'] for c in fixed['cases'] if c['set'] == 'S'] != ['V04-V06-AssemblyNgPending']:
        raise ValueError('SharedRepresentativeChanged')
    return dict(migration=review, coverage=coverage, inputsReviewedAt=now(),
                inputHashes={i['path']: sha(ROOT / i['path']) for i in inputs})


def phase_environment(folder, context, fixed, phase):
    env = dict(os.environ, PYTHONUTF8='1', PYTHONDONTWRITEBYTECODE='1', DOTNET_NOLOGO='1',
               GAODE_ENVIRONMENT='Test', GAODE_TEST_ROOT=str(folder / 'test-data'),
               GAODE_009_INTEGRATION_ROOT=str(folder / 'test-data' / 'component-routes'),
               GAODE_009_EVIDENCE_ROOT=str(folder / 'native'), GAODE_010_EVIDENCE_ROOT=str(folder / 'native'),
               GAODE_010_ATTEMPT=context['verificationAttemptId'], GAODE_010_PYTHON=sys.executable,
               GAODE_010_WORKER_EVIDENCE=str(folder / 'worker-selfcheck'))
    if phase in ('B', 'E'):
        env.update(GAODE_010_FULLRUN_ROOT=str(folder / 'full-run'),
                   GAODE_010_FIXTURE=str(ROOT / fixed['fullRunFixtures'][phase]))
    return env


def run_phase(parent, phase, fixed, frozen=None):
    import runner
    folder = ROOT / 'artifacts/recipe-execution-010' / parent / phase
    folder.mkdir(parents=True, exist_ok=False)
    cases = [c for c in fixed['cases'] if c['set'] == phase]
    commands = []
    started = now()
    before = source_files()
    # V00 is actual build execution, not inference from existing DLLs. StorePrep
    # is a project dependency of Integration.Tests and is fingerprinted as well.
    for case in [c for c in cases if c['kind'] == 'build']:
        cmd = ['dotnet', 'build', case['project'], '--no-restore']
        log = case['caseId'] + '.log'; start = now()
        code = runner.run_logged(cmd, folder / log, dict(os.environ, DOTNET_NOLOGO='1'), timeout=1200)
        commands.append(dict(kind='build', caseIds=[case['caseId']], command=cmd,
                             startedAt=start, endedAt=now(), exitCode=code, report=log, log=log))
        if code: break
    context = make_context(parent, 'RecipeExecution010', ROOT / REQUIRED, light=False)
    context['startedAt'] = started
    if before != context['sourceFiles']: raise ValueError('SourceChangedDuringBuild')
    if frozen: require_frozen(frozen)
    write_json(folder / 'context.json', context)
    write_json(folder / 'fixed-cases.json', cases)
    for command in commands: command['context'] = identity(context)
    env = phase_environment(folder, context, fixed, phase)
    if not any(c['exitCode'] for c in commands):
        worker = [c for c in cases if c['kind'] == 'worker-selfcheck']
        if worker:
            start = now(); cmd = [sys.executable, '-B', 'scripts/tests/test_010_content_sample_worker.py']
            code = runner.run_logged(cmd, folder / 'worker-selfcheck.log', env, timeout=300)
            commands.append(dict(kind='worker-selfcheck', caseIds=[c['caseId'] for c in worker], context=identity(context),
                command=cmd, startedAt=start, endedAt=now(), exitCode=code, report='worker-selfcheck.log'))
        # Discover/execute fixed independent identities. Expose the first formal
        # chain before the longer component regressions, with no loss of B duties.
        groups = sorted({(c['verification'], c['suite']) for c in cases if c['kind'] == 'dotnet'},
                        key=lambda k: ({'V01': 0, 'V02': 1, 'V05': 2, 'V03': 3, 'V04': 4, 'V06': 5}[k[0]], k[1]))
        for verification, suite in groups:
            if any(c['exitCode'] for c in commands): break
            selected = [c for c in cases if c['kind'] == 'dotnet' and c['verification'] == verification and c['suite'] == suite]
            command = execute_dotnet(folder, context, selected, env, verification + '-' + suite)
            commands.append(command)
            if command['exitCode'] == 0:
                rows, errors = trx_rows(folder / command['report'], folder / command['discovery'], selected, context)
                checked = validate_rows(selected, rows, context, errors)
                if checked['result'] != 'Passed':
                    write_json(folder / (verification + '-' + suite + '.rejection.json'), checked)
                    break
    return seal_bundle(folder, context, commands, [c['caseId'] for c in cases])


def require_frozen(frozen):
    if (source_files() != frozen['sourceFiles'] or build_files(False) != frozen['buildFiles']
            or sha(ROOT / REQUIRED) != frozen['manifestDigest']
            or sha(ROOT / LIGHT) != frozen['lightManifestDigest']):
        raise ValueError('FreezeInvalidated:rebuild-affected-baseline-and-freeze-required')
    return True


def freeze_baseline(parent, baseline, credential, prepared):
    if baseline['result'] != 'Passed': raise ValueError('BaselineRequiredBeforeFreeze')
    current = validate_bundle(baseline['path'], parent, 'RecipeExecution010')
    gate = final_gate(True, credential, parent, 'RecipeExecution010', 'freeze')
    if current['result'] != 'Passed' or not gate['passed']: raise ValueError('BaselineOrLightweightInvalidated')
    context = current['context']
    native = read_json(Path(credential) / 'native/recipe-boundary.json')
    frozen = dict(schemaVersion='010-freeze/1', parentAttemptId=parent, frozenAt=now(),
        baselineAttemptId=context['verificationAttemptId'], baselineContextDigest=current['contextDigest'],
        sourceFiles=context['sourceFiles'], buildFiles=context['buildFiles'], manifestDigest=context['manifestDigest'],
        lightManifestDigest=sha(ROOT / LIGHT), responsibilities=native['closure'],
        inputsPreparedBeforeBaseline=prepared, note='Superset includes business helpers, contracts, budget, independent oracles and manifests')
    require_frozen(frozen)
    write_json(ROOT / 'artifacts/recipe-execution-010' / parent / 'freeze.json', frozen)
    return frozen


def compare_replacements(folder, frozen):
    require_frozen(frozen)
    base = read_json(folder / 'B/full-run/obligations.json')
    equivalent = read_json(folder / 'E/full-run/obligations.json')
    if base['runId'] == equivalent['runId']: raise ValueError('ReplacementNeedsIndependentRun')
    if (base['implementation'] != 'PythonWorkerAdapter/1' or equivalent['implementation'] != 'ContentSampleWorker/1'
            or not base['complete'] or not equivalent['complete']):
        raise ValueError('ActualImplementationOrCompletionMissing')
    for name in ('algorithmFacts', 'mediaCount', 'source', 'obligations'):
        if base[name] != equivalent[name]: raise ValueError('NecessaryBusinessObligationsDiffer:' + name)
    # Both TRX runs independently assert the fixed 111mm coordinates, action
    # order, judgments, committed identities and saves against reviewed literals.
    # E also verifies the actual MediaRead/Result/InputReleased process audit.
    return dict(result='Passed', baselineRunId=base['runId'], equivalentRunId=equivalent['runId'],
                commonSourceChanges=0, boundaries=frozen['inputsPreparedBeforeBaseline']['coverage']['equivalent'],
                evidence=['B/full-run/obligations.json', 'E/full-run/obligations.json',
                          'E/full-run/store/media-root/worker-protocol.jsonl'],
                allowedDifferences=['runId', 'time', 'real-source-reference', 'environment-implementation', 'recipe-identity'])


def close_profile(parent, credential, frozen, fixed):
    import migration_audit
    folder = ROOT / 'artifacts/recipe-execution-010' / parent
    require_frozen(frozen)
    phases = {phase: validate_bundle(folder / phase, parent, 'RecipeExecution010') for phase in ('B', 'E', 'S')}
    errors = [phase + ':' + error for phase, value in phases.items() for error in value['errors']]
    for phase, value in phases.items():
        required = {c['caseId'] for c in fixed['cases'] if c['set'] == phase}
        if {r['caseId'] for r in value['rows']} != required: errors.append('FinalPhaseCoverageMissing:' + phase)
        if phase != 'B' and instant(value['context']['startedAt']) < instant(frozen['frozenAt']):
            errors.append('ReplacementExecutedBeforeFreeze:' + phase)
    observed = [r for value in phases.values() for r in value['rows']]
    if len({r['caseId'] for r in observed}) != len(fixed['cases']): errors.append('FinalMissingOrDuplicateCase')
    audit = migration_audit.audit010(ROOT, read_json(ROOT / REGISTRY), fixed, observed)
    errors += audit['errors']
    gate = final_gate(True, credential, parent, 'RecipeExecution010', 'V07')
    if not gate['passed']: errors += gate['lightweight']['errors']
    replacement = compare_replacements(folder, frozen) if not errors else None
    result = dict(passed=not errors, scope='RecipeExecution010', evidence=str(folder.relative_to(ROOT)),
        finalSet='B union E union S; V07 is this closure, not a prerequisite result',
        errors=errors, requiredCount=len(fixed['cases']), observedCount=len(observed),
        phaseCounts={p: len(v['rows']) for p, v in phases.items()}, migration=audit, replacement=replacement,
        identities=[dict(identity(v['context']), caseIds=[r['caseId'] for r in v['rows']]) for v in phases.values()],
        scopeLimits=fixed['scopeLimits'], source_digest=runner_source_digest())
    write_json(folder / 'V07.json', result)
    return result


def runner_source_digest():
    import runner
    return runner.source_digest()


def run_profile(parent, credential):
    """One public invocation, fixed phase order, no skip/filter/force controls."""
    folder = ROOT / 'artifacts/recipe-execution-010' / parent
    try:
        fixed = manifest(ROOT / REQUIRED)
        prepared = review_prepared_inputs(fixed)
        write_json(folder / 'preflight.json', prepared)
        baseline = run_phase(parent, 'B', fixed)
        if baseline['result'] != 'Passed':
            return dict(passed=False, scope='RecipeExecution010', failedPhase='B', errors=baseline['errors'], evidence=str(folder.relative_to(ROOT)))
        frozen = freeze_baseline(parent, baseline, credential, prepared)
        for phase in ('E', 'S'):
            require_frozen(frozen)
            result = run_phase(parent, phase, fixed, frozen)
            if result['result'] != 'Passed':
                return dict(passed=False, scope='RecipeExecution010', failedPhase=phase, errors=result['errors'], evidence=str(folder.relative_to(ROOT)))
        return close_profile(parent, credential, frozen, fixed)
    except (OSError, ValueError, KeyError, TypeError, ET.ParseError) as error:
        failed = dict(passed=False, scope='RecipeExecution010', errors=[type(error).__name__ + ':' + str(error)], evidence=str(folder.relative_to(ROOT)))
        write_json(folder / 'profile-rejected.json', failed)
        return failed
