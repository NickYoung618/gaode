"""Project-local adapter for the installed Spec Kit workflow engine; no product imports."""
from __future__ import annotations
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import uuid
import xml.etree.ElementTree as ET
from locks import file_lock
import recipe_execution_010 as recipe010

ROOT = Path(__file__).resolve().parents[2]
WF = ROOT / ".specify" / "workflows"
YAML = ROOT / "workflows" / "auto-dev.yml"
STATION = "001-station01-public-preparation"
M1 = [f"T{i:03d}" for i in [*range(1, 26), *range(30, 39)]]
SUITES = ("Gaode.Rules.Tests", "Gaode.Contracts.Tests", "Gaode.Integration.Tests", "Gaode.Communication.Tests")


def stamp():
    return datetime.now(timezone.utc).isoformat()


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def save(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(path.name + ".tmp")
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    tmp.replace(path)


def inside(path, parent=ROOT):
    resolved = Path(path).resolve()
    if not resolved.is_relative_to(parent.resolve()):
        raise ValueError(f"路径超出允许目录：{path}")
    return resolved


def feature_path(name):
    if not re.fullmatch(r"\d{3,}-[a-z0-9]+(?:-[a-z0-9]+)*", name or ""):
        raise ValueError("Feature必须为编号及小写英文，例如002-history-export。")
    return inside(ROOT / "specs" / name, ROOT / "specs")


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def task_rows(path):
    text = Path(path).read_text(encoding="utf-8-sig")
    matches = list(re.finditer(r"(?m)^- \[([ xX])\] (T\d{3,})\b[^\n]*", text))
    rows = {}
    for i, m in enumerate(matches):
        if m[2] in rows:
            raise ValueError("任务ID重复：" + m[2])
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        block = re.split(r"(?m)^## ", text[m.start():end], maxsplit=1)[0].strip()
        dep_line = re.search(r"前置：([^。\n]*)", block)
        deps = re.findall(r"T\d{3,}", dep_line[1]) if dep_line else []
        canonical = re.sub(r"^- \[[ xX]\]", "- [ ]", block)
        rows[m[2]] = dict(checked=m[1].lower() == "x", deps=deps,
                         contract=hashlib.sha256(canonical.encode()).hexdigest())
    if not rows:
        raise ValueError("tasks.md未包含可执行的T编号任务。")
    return rows


def select_tasks(feature, milestone, rows):
    selected = M1 if feature == STATION and milestone == "M1" else list(rows)
    missing = set(selected) - rows.keys()
    if missing:
        raise ValueError("任务缺失：" + ",".join(sorted(missing)))
    visiting, visited = set(), set()
    def walk(key):
        if key in visiting or key not in rows:
            raise ValueError("任务依赖循环或不存在：" + key)
        if key in visited:
            return
        visiting.add(key)
        for dep in rows[key]["deps"]:
            walk(dep)
        visiting.remove(key)
        visited.add(key)
    for key in selected:
        walk(key)
    if not visited.issubset(set(selected)):
        raise ValueError("所选里程碑不构成完整依赖闭包。")
    return selected


def source_digest():
    paths = [ROOT / "global.json"]
    for base in ('backend','VirtualPlc','frontend','scripts','specs','desktop','packaging','workflows'):
        directory=ROOT/base
        if directory.exists():
            paths += [p for p in directory.rglob('*') if p.is_file()
                and not {'obj','bin','.git','node_modules','dist','__pycache__'}.intersection(p.relative_to(directory).parts)
                and p.name!='tasks.md' and 'checklists' not in p.parts
                and p.suffix.lower() in {'.cs','.csproj','.props','.targets','.json','.slnx','.py','.js','.cjs','.mjs','.ts','.ps1','.psm1','.md','.yaml','.yml'}]
    h = hashlib.sha256()
    for p in sorted(set(paths)):
        if p.exists():
            h.update(str(p.relative_to(ROOT)).encode())
            h.update(p.read_bytes())
    return h.hexdigest()


def required_build_binaries():
    return [f'backend/tests/{suite}/bin/Debug/net10.0/{suite}.dll' for suite in SUITES] + [
        'backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll',
        'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll',
        'backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll']


def build_identity():
    """Hash actual outputs, including loaded dependencies, not just entry DLLs."""
    required = [ROOT / p for p in required_build_binaries()]
    missing = [str(p.relative_to(ROOT)) for p in required if not p.is_file()]
    if missing:
        raise ValueError('RequiredBuildOutputMissing:' + ','.join(missing))
    paths = set()
    for binary in required:
        paths.update(p for p in binary.parent.rglob('*') if p.is_file()
                     and p.suffix.lower() in ('.dll', '.json')
                     and not {'test-data', 'component-routes'}.intersection(p.relative_to(binary.parent).parts))
    files = [dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p)) for p in sorted(paths)]
    value = hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    return dict(buildDigest=value, files=files)


def required_manifest():
    path=ROOT/'scripts/workflow/009-required-cases.json'
    value=load(path)
    cases=value.get('cases',[])
    ids=[c['caseId'] for c in cases]
    if value.get('schemaVersion')!='009-required-cases/1' or not ids or len(ids)!=len(set(ids)):
        raise ValueError('RequiredManifestInvalid')
    for relative in value.get('requiredFiles',[]):
        if not inside(ROOT/relative).is_file(): raise ValueError('RequiredScannerMissing:'+relative)
    return value


def validate_required_ledger(manifest, observed, context, scope='verify'):
    """The authoritative expectation is the checked-in manifest, never test discovery."""
    expected=[c for c in manifest['cases'] if c['scope']==scope]
    failures=[]
    ids=[row['caseId'] for row in observed]
    if not expected: failures.append(dict(code='G01',caseId=scope,reason='RequiredGroupMissing'))
    for case in expected:
        rows=[r for r in observed if r['caseId']==case['caseId']]
        reason=None
        if len(rows)!=1: reason='MissingOrDuplicateCase'
        else:
            row=rows[0]
            if not row.get('discovered'): reason='NotDiscovered'
            elif not row.get('executed'): reason='NotExecutedOrFiltered'
            elif row.get('outcome')!='Passed': reason='FailedOrSkipped'
            elif any(row.get(k)!=context[k] for k in ('runId','sourceDigest','manifestDigest','buildDigest')): reason='StaleOrUnboundReport'
            elif not set(case.get('requiredEvidence',[])).issubset(row.get('evidenceKinds',[])): reason='RequiredEvidenceMissing'
        if reason: failures.append(dict(code='REQUIRED-CASE',caseId=case['caseId'],reason=reason))
    return dict(result='Rejected' if failures else 'Passed',scope=scope,requiredCount=len(expected),
                observedCount=len(ids),failures=failures,cases=observed)


def test_identity(name):
    match=re.match(r'^(.*?)\(caseId: "([^"]+)"(?:,|\))',name)
    return (match[1],match[2]) if match else (name,None)


def collect_required_ledger(evidence, manifest, context, script_report, selfcheck_report, commands):
    from native_evidence import index as native_index, collect as collect_native
    native_records=native_index(script_report.parent)
    observed=[]
    expected=manifest['cases']
    def row(case_id,discovered,executed,outcome,kinds=()):
        observed.append(dict(context,caseId=case_id,discovered=discovered,executed=executed,
                             outcome=outcome,evidenceKinds=list(kinds)))
    for suite in SUITES:
        discovery=evidence/(suite+'.discovery.log')
        trx=evidence/(suite+'.trx')
        if not discovery.is_file() or not trx.is_file(): continue
        discovered={test_identity(x.strip()) for x in discovery.read_text(encoding='utf-8-sig').splitlines()}
        try:
            tree=ET.parse(trx)
            times=tree.find('.//{*}Times')
            if times is None or datetime.fromisoformat(times.attrib['start'].replace('Z','+00:00')) < datetime.fromisoformat(context['startedAt']):
                raise ValueError('TRXPrecedesCurrentRun')
            for result in tree.findall('.//{*}UnitTestResult'):
                identity=test_identity(result.attrib.get('testName',''))
                for case in expected:
                    if case.get('suite')!=suite or (case.get('method'),case.get('dataRow'))!=identity: continue
                    kinds=['trx']
                    attachment=evidence/(case['caseId'].replace('/','--')+'.evidence.json')
                    if not attachment.exists():
                        collect_native(case,result.attrib,native_records,evidence,context)
                    if attachment.is_file():
                        proof=load(attachment)
                        if all(proof.get(k)==context[k] for k in context) and proof.get('caseId')==case['caseId']:
                            for item in proof.get('files',[]):
                                path=inside(evidence/item['path'],evidence)
                                if path.is_file() and digest(path)==item['sha256']: kinds.append(item['kind'])
                    row(case['caseId'],identity in discovered,True,result.attrib.get('outcome'),kinds)
        except (ET.ParseError,ValueError,OSError,KeyError):
            row('TRX-PARSE/'+suite,False,False,'Error')
    if script_report.is_file():
        report=load(script_report)
        inventory_path=ROOT/'backend/tests/Gaode.Rules.Tests/Architecture/009-boundary-inventory.json'
        inventory=load(inventory_path)
        h=hashlib.sha256()
        names={r['path'] for r in inventory['files']} | {'scripts/architecture/009-script-boundary-cases.json'}
        for name in sorted(names):
            p=ROOT/name
            if p.is_file(): h.update(name.encode());h.update(p.read_bytes())
        valid=(report.get('runId')==context['runId'] and report.get('sourceDigest')==h.hexdigest()
            and report.get('inventoryDigest')==digest(inventory_path) and report.get('scope')=='RepositoryAndCases'
            and report.get('parserVersions')==inventory['scriptParserVersions'])
        scanned={r['path'] for r in report.get('files',[]) if r.get('parsed')}
        for case in report.get('cases',[]):
            row(case['caseId'],valid,valid,case['outcome'],['ast-case'])
        for suffix,extensions in [('JS',{'.js','.cjs','.mjs','.ts'}),('PY',{'.py'}),('PS',{'.ps1','.psm1'})]:
            needed={r['path'] for r in inventory['files'] if Path(r['path']).suffix in extensions}
            errors=[v for v in report.get('errors',[])+report.get('violations',[]) if Path(v.get('path','')).suffix in extensions]
            complete=valid and bool(needed) and needed.issubset(scanned)
            row('SCRIPT-SOURCE-'+suffix,complete,complete,'Passed' if complete and not errors else 'Rejected',['ast-source'])
        row('SCRIPT-INVENTORY',valid,valid,'Passed' if valid and not report.get('errors') else 'Rejected',['classification'])
    if selfcheck_report.is_file():
        report=load(selfcheck_report)
        valid=all(report.get(k)==context[k] for k in ('runId','sourceDigest','manifestDigest'))
        for case in report.get('cases',[]):
            row(case['caseId'],valid,valid,case['outcome'],['runner-selfcheck'])
    components=script_report.parent/'validator-components.json'
    if components.is_file():
        try:
            report=load(components)
            engine=report.get('workflowEngine', {})
            engine_valid=(not manifest.get('requiredWorkflowEngineVersion') or
                engine.get('distribution')=='specify-cli' and
                engine.get('version')==manifest['requiredWorkflowEngineVersion'] and
                Path(engine.get('path','')).is_file() and
                digest(Path(engine['path']))==engine.get('sha256'))
            valid=(all(report.get(k)==context[k] for k in ('runId','sourceDigest','manifestDigest','buildDigest'))
                and engine_valid
                and report.get('scope')=='VerifierComponentsOnly'
                and report.get('schemaVersion')=='009-validator-components/1' and report.get('result')=='Passed'
                and datetime.fromisoformat(report['startedAt'])>=datetime.fromisoformat(context['startedAt'])
                and any(c.get('kind')=='script-components' and c.get('exit_code')==0 for c in commands)
                and set(report.get('reports',{}))=={'validator-python.log','validator-node.log','validator-node.xml'} and all(
                    inside(components.parent/name,components.parent).is_file() and
                    digest(inside(components.parent/name,components.parent))==expected_digest
                    for name,expected_digest in report['reports'].items()))
            for case in report.get('cases',[]):
                row(case['caseId'],valid and case.get('discovered') is True,
                    valid and case.get('executed') is True,case['outcome'],['script-component'])
        except (ValueError,OSError,KeyError,TypeError):
            row('SCRIPT-COMP/PARSE',False,False,'Error')
    from migration_audit import audit as audit_migration
    try:
        migration=audit_migration(ROOT,load(ROOT/'backend/tests/Gaode.Rules.Tests/Architecture/009-test-obligations.json'),
                                  manifest,observed,context)
        save(evidence/'migration-audit.json',migration)
        row('MIGRATION-SOURCE-AND-EXECUTION',True,True,migration['result'],['migration-audit'])
    except (ValueError,OSError,KeyError,TypeError):
        row('MIGRATION-SOURCE-AND-EXECUTION',False,False,'Error')
    result=validate_required_ledger(manifest,observed,context)
    result.update(context)
    result['pendingProcessCases']=[x['caseId'] for x in expected if x['scope']=='integration']
    result['overall009Passed']=False  # Only T054/T061 may merge the independent-process evidence.
    return result


def protected_files(feature):
    paths = [ROOT / "dev.ps1", ROOT / "scripts" / "verify.ps1",
             ROOT / "scripts" / "workflow" / "verify_entry.py"]
    paths += [p for p in (ROOT / "dev-parallel.ps1", ROOT / "workspace.json") if p.is_file()]
    for base in (ROOT / ".specify" / "memory", ROOT / ".specify" / "templates",
                 ROOT / "软件需求规格说明书", ROOT / "specs",
                 ROOT / "scripts" / "workflow", ROOT / "workflows"):
        if base.exists():
            paths += [p for p in base.rglob("*") if p.is_file()
                      and "__pycache__" not in p.parts
                      and not p.resolve().is_relative_to(feature.resolve())]
    return {str(p.relative_to(ROOT)): digest(p) for p in paths}


def verify_protected(request, control):
    for rel, expected in control.get("protected", {}).items():
        p = inside(ROOT / rel)
        if not p.is_file() or digest(p) != expected:
            raise ValueError("受保护基线发生变化，需人工核对，不自动恢复文件：" + rel)
    frozen = control.get("task_baseline")
    if frozen:
        rows = task_rows(feature_path(request["feature"]) / "tasks.md")
        if set(rows) != set(frozen):
            raise ValueError("任务ID集合变化；不得在实现/修复循环偷偷增删任务。")
        for key, prior in frozen.items():
            if rows[key]["contract"] != prior["contract"]:
                raise ValueError("任务定义变化：" + key)
            if key not in control["selected"] and rows[key]["checked"] != prior["checked"]:
                raise ValueError("修改了里程碑外任务状态：" + key)


def context():
    env = os.environ.get("GAODE_WORKFLOW_REQUEST")
    if not env:
        raise ValueError("缺少Workflow上下文，请通过dev.ps1启动。")
    request_file = inside(env, WF / "requests")
    request = load(request_file)
    if request["request_id"] != request_file.parent.name:
        raise ValueError("请求身份不匹配。")
    return request, request_file.parent, load(request_file.parent / "control.json")


def freeze(request, folder, control):
    feature = feature_path(request["feature"])
    required = ("spec.md", "plan.md", "tasks.md")
    for name in required:
        if not (feature / name).is_file():
            raise ValueError("缺少阶段产物：" + name)
    rows = task_rows(feature / "tasks.md")
    selected = select_tasks(request["feature"], request["milestone"], rows)
    control["task_baseline"], control["selected"] = rows, selected
    for p in feature.rglob("*"):
        if p.is_file() and p.name != "tasks.md":
            control["protected"][str(p.relative_to(ROOT))] = digest(p)
    save(folder / "control.json", control)


def check_report(request, folder, control):
    from prompt_stage import validate_report
    expected = control["current"]
    report_file = inside(folder / expected["report"], folder)
    return validate_report(report_file.read_text(encoding="utf-8-sig"), expected, ROOT)


def read_trx(path):
    tree = ET.parse(path)
    counters = tree.find(".//{*}Counters")
    if counters is None:
        raise ValueError("TRX缺少Counters，不能判为测试通过。")
    c = {k: int(v) for k, v in counters.attrib.items()}
    total = c.get("total", 0)
    failures = sum(c.get(name, 0) for name in ("failed", "error", "blocked", "inconclusive", "aborted", "notExecuted"))
    return (total > 0 and c.get("passed", 0) == total and c.get("executed", 0) == total
            and failures == 0)


@contextmanager
def heartbeat(folder, label):
    """The engine captures shell stderr; emit progress from its parent as well."""
    stopped = threading.Event()
    started = time.monotonic()
    def announce():
        while not stopped.wait(20):
            try:
                files = list((folder / "execution-logs").glob("*.execution.json"))
                latest = load(max(files, key=lambda p: p.stat().st_mtime)) if files else {}
                if (folder / "control.json").is_file():
                    activity = load(folder / "control.json").get("activity", {})
                    if activity:
                        latest = activity
                detail = f"phase={latest.get('phase', 'preflight')}; status={latest.get('status', 'waiting')}"
                print(f"[{label}] elapsed={int(time.monotonic()-started)}s; {detail}; logs={folder}",
                      file=sys.stderr, flush=True)
            except (OSError, ValueError):
                print(f"[{label}] 执行中；logs={folder}", file=sys.stderr, flush=True)
    thread = threading.Thread(target=announce, daemon=True)
    thread.start()
    try:
        yield
    finally:
        stopped.set()
        thread.join(timeout=2)


def run_logged(command, log, env, timeout=1200):
    """Bounded build/test child with progress and owned-descendant cleanup."""
    from execution import ProcessJob
    started = time.monotonic()
    process, job = None, None
    try:
        with log.open("xb") as out:
            process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=out,
                                       stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL)
            job = ProcessJob(process)
            while True:
                remaining = timeout - (time.monotonic() - started)
                if remaining <= 0:
                    return 124
                try:
                    return process.wait(timeout=min(20, remaining))
                except subprocess.TimeoutExpired:
                    print(f"[Verify] {' '.join(command[:3])}; elapsed={int(time.monotonic()-started)}s; log={log}",
                          file=sys.stderr, flush=True)
    finally:
        if job:
            job.close()
        if process and process.poll() is None:
            process.kill()
            process.wait(timeout=10)


def coordination_root():
    marker = ROOT / "workspace.json"
    if not marker.exists():
        return ROOT
    data = load(marker)
    origin = Path(data.get("source_root", "")).resolve()
    if (data.get("schema") != 1 or data.get("status") != "ready"
            or Path(data.get("workspace_root", "")).resolve() != ROOT.resolve()
            or origin == ROOT.resolve() or not (origin / "dev.ps1").is_file()
            or (origin / "workspace.json").exists()):
        raise ValueError("并行工作区身份/源目录无效，拒绝绕过共享Verify锁")
    return origin


def run_verify(request, folder, control, *, wait_seconds=600):
    coordinator = coordination_root()
    queued = stamp()
    with file_lock(coordinator / ".specify" / "workflows" / "verification.lock",
                   workspace=ROOT, wait_seconds=wait_seconds):
        return _run_verify(request, folder, control, queued, str(coordinator))


def create_test_root(evidence, request_id, iteration, configured=None):
    if configured:
        parent = Path(configured)
        temporary = Path(tempfile.gettempdir()).resolve()
        if not parent.is_absolute() or parent.resolve() == temporary or not parent.resolve().is_relative_to(temporary):
            raise ValueError('TestParentOutsideUserTemporaryDirectory')
        if any(p.is_symlink() or getattr(p, 'is_junction', lambda: False)() for p in (parent, *parent.parents)):
            raise ValueError('LinkedTestParentRejected')
        if not re.fullmatch(r'(?:standalone-)?[0-9a-f]{32}', request_id) or iteration not in (1, 2, 3):
            raise ValueError('InvalidTestRunIdentity')
        run_root = parent / request_id / f'verify-{iteration:02d}'
        if any(p.is_symlink() or getattr(p, 'is_junction', lambda: False)() for p in (run_root, *run_root.parents)):
            raise ValueError('LinkedTestRunRootRejected')
        # mkdir(exist_ok=False) also rejects an existing file/link: never reuse a store.
        run_root.mkdir(parents=True, exist_ok=False)
        test_root = run_root / 'test-data'
    else:
        test_root = evidence / 'test-data'
    test_root.mkdir()
    save(test_root / 'purpose.json', {'purpose': 'Test', 'request_id': request_id,
                                    'actualTestRoot': str(test_root.resolve())})
    return test_root


def verification_timeout(kind):
    # Harness ceilings only. Actual device/save/recipe deadlines remain frozen
    # product inputs. The complete integration suite takes over an hour.
    return 10800 if kind == 'test:Gaode.Integration.Tests' else 1200


def _run_verify(request, folder, control, queued, coordinator):
    profile = request.get('profile') or ('RecipeExecution010' if request.get('feature') ==
        '010-recipe-execution-isolation' else 'Default009')
    if profile not in ('RecipeExecution010', 'Default009', 'PlcPolling013'):
        raise ValueError('UnknownVerificationProfile:' + profile)
    if profile != 'RecipeExecution010' and control.get('verification_count', 0) >= 3:
        raise ValueError('三轮总验证已用尽；不自动追加验证/修复轮次')
    parent = request['request_id'] + '-verify-' + str(control.get('verification_count', 0) + 1)
    def own(credential):
        if profile == 'PlcPolling013':
            import plc_polling_013
            control['verification_count'] = control.get('verification_count', 0) + 1
            return plc_polling_013.run_profile(parent, credential)
        if profile == 'RecipeExecution010':
            control['verification_count'] = control.get('verification_count', 0) + 1
            return recipe010.run_profile(parent, credential)
        return _run_default_verify(request, folder, control, queued, coordinator, credential, parent, profile)
    checked = recipe010.run_with_lightweight(parent, profile, own)
    result = dict(checked.get('ownResult') or {}, passed=checked['passed'],
        lightweight_credential=checked['credential'], acceptance_parent=parent, profile=profile)
    summary = {**control.get('verification', {}), **result, 'source_digest': source_digest()}
    control['verification'] = summary
    control['activity'] = dict(phase='verify', status='passed' if result['passed'] else 'failed')
    save(folder / 'control.json', control)
    recipe010.write_json(ROOT / 'artifacts/recipe-execution-010' / parent / 'acceptance-result.json', result)
    return result


def _run_default_verify(request, folder, control, queued, coordinator, credential, parent, profile):
    started_at = stamp()
    iteration = control.get("verification_count", 0) + 1
    if iteration > 3:
        raise ValueError("三轮总验证已用尽；不自动追加验证/修复轮次")
    control["verification_count"] = iteration
    control["activity"] = dict(phase="verify", status="running", iteration=iteration)
    save(folder / "control.json", control)
    evidence = inside(ROOT / "artifacts" / "workflow" / request["request_id"] / f"verify-{iteration:02d}")
    evidence.mkdir(parents=True, exist_ok=False)
    test_root = create_test_root(evidence, request['request_id'], iteration,
                                os.environ.get('GAODE_VERIFY_TEST_PARENT'))
    env = os.environ.copy()
    env.update(GAODE_TEST_ROOT=str(test_root), GAODE_ENVIRONMENT="Test",
               GAODE_009_INTEGRATION_ROOT=str(test_root/'component-routes'),
               DOTNET_NOLOGO="1", DOTNET_CLI_TELEMETRY_OPTOUT="1")
    before = source_digest()
    results = []
    manifest_path=ROOT/'scripts/workflow/009-required-cases.json'
    manifest=required_manifest()
    gate_root=ROOT/'artifacts/recipe-execution-008/009-isolation'/request['request_id']/f'verify-{iteration:02d}'
    gate_root.mkdir(parents=True,exist_ok=False)
    script_report=gate_root/'script-boundary.json'
    selfcheck_report=gate_root/'ledger-selfcheck.json'
    env['GAODE_009_EVIDENCE_ROOT']=str(gate_root)
    # The migrated formal-chain method requires explicit controlled inputs even
    # when exercised by the old 009 local suite. It remains LocalVerifyOnly.
    env.update(GAODE_010_FULLRUN_ROOT=str(ROOT/'artifacts/recipe-execution-010'/parent/'default-profile-fullrun'),
        GAODE_010_FIXTURE=str(ROOT/'backend/tests/Gaode.Integration.Tests/Fixtures/RecipeExecution010/baseline.json'),
        GAODE_010_PYTHON=sys.executable)
    context009=dict(runId=request['request_id'],sourceDigest=before,manifestDigest=digest(manifest_path),
                    buildDigest='BuildNotExecuted',startedAt=started_at)
    commands = [
        ([sys.executable,'-B','scripts/workflow/test_verify.py','--ledger-selfcheck','--output',str(selfcheck_report),
          '--run-id',request['request_id'],'--source-digest',before,'--manifest-digest',context009['manifestDigest']], 'selfcheck'),
        ([sys.executable,'-B','scripts/check-009-script-boundary.py','--run-id',request['request_id'],
          '--output',str(script_report)], 'scripts'),
        (["dotnet", "restore", "backend/Gaode.slnx"], 'restore'),
        (["dotnet", "build", "backend/Gaode.slnx", "--no-restore"], 'build'),
        ([sys.executable,'-B','scripts/workflow/validator_components.py','--output',
          str(gate_root/'validator-components.json')], 'script-components'),
    ]
    for suite in SUITES:
        base=["dotnet", "test", f"backend/tests/{suite}/{suite}.csproj", "--no-build", "--no-restore"]
        commands.append((base+['--list-tests'],'discover:'+suite))
        commands.append((base+["--logger",f"trx;LogFileName={suite}.trx","--results-directory",str(evidence),
            '--blame-hang-timeout','15min','--blame-hang-dump-type','none'], 'test:'+suite))
    passed = True
    build_source = None
    for i, (command,kind) in enumerate(commands):
        if kind == 'build':
            build_source = source_digest()
            context009['sourceDigest']=build_source
        log = evidence / (kind.split(':')[1]+'.discovery.log' if kind.startswith('discover:') else f"{i + 1:02d}.log")
        started = time.monotonic()
        print(f"[Verify {i + 1}/{len(commands)}] {' '.join(command[:3])}", file=sys.stderr, flush=True)
        try:
            code = run_logged(command, log, env, timeout=verification_timeout(kind))
        except OSError as exc:
            with log.open("a", encoding="utf-8") as out: out.write(str(exc))
            code = 127
        item = dict(command=command, kind=kind, exit_code=code, log=str(log.relative_to(ROOT)),
                    elapsed_seconds=round(time.monotonic()-started, 3), timeout_seconds=verification_timeout(kind))
        if code:
            item["failure_kind"] = "verification_timeout" if code == 124 else (
                "verification_tool_unavailable" if code == 127 else "verification_command_failed")
        if kind.startswith('test:'):
            trx = evidence / f"{kind.split(':')[1]}.trx"
            try:
                item["tests_all_passed"] = read_trx(trx)
                item["test_counts"] = {k: int(v) for k, v in ET.parse(trx).find(".//{*}Counters").attrib.items()}
            except (OSError, ValueError, ET.ParseError): item["tests_all_passed"] = False
            if not item["tests_all_passed"]: item["failure_kind"] = "test_evidence_failed"
        success=code==0 and (not kind.startswith('test:') or item.get('tests_all_passed') is True)
        passed &= success
        results.append(item)
        if kind=='build' and success:
            try:
                actual_build=build_identity()
                save(evidence/'009-build-files.json',actual_build)
                context009['buildDigest']=actual_build['buildDigest']
            except (OSError, ValueError):
                passed=False
            env.update(GAODE_009_RUN_ID=context009['runId'],GAODE_009_SOURCE_DIGEST=context009['sourceDigest'],
                       GAODE_009_MANIFEST_DIGEST=context009['manifestDigest'],GAODE_009_BUILD_DIGEST=context009['buildDigest'],
                       GAODE_009_CASE_EVIDENCE_ROOT=str(evidence))
        # Script rejection must not suppress the independent C# source scan.
        if not success and kind not in ('selfcheck','scripts'): break
    ledger=collect_required_ledger(evidence,manifest,context009,script_report,selfcheck_report,results)
    save(evidence/'009-execution-ledger.json',ledger)
    passed &= ledger['result']=='Passed' and context009['buildDigest']!='BuildNotExecuted'
    after = source_digest()
    try:
        passed &= build_identity()['buildDigest'] == context009['buildDigest']
    except (OSError, ValueError):
        passed=False
    # Restore may create lock files; code must remain unchanged from build through verification.
    passed &= build_source is not None and build_source == after
    passed = recipe010.final_gate(bool(passed), credential, parent, profile, "_run_verify")["passed"]
    summary = dict(lightweight_credential=credential, acceptance_parent=parent, profile=profile, passed=bool(passed), scope="LocalVerifyOnly", overall009Passed=False, required_ledger="009-execution-ledger.json", commands=results, source_digest=after,
                   source_before_restore=before, source_at_build=build_source,
                   test_root=str(test_root.relative_to(ROOT)) if test_root.is_relative_to(ROOT) else str(test_root),
                   actual_test_root=str(test_root.resolve()), time=stamp(),
                   component_route_root=str((test_root/'component-routes').resolve()),
                   queued_at=queued, started_at=started_at, coordination_root=coordinator)
    save(evidence / "verification.json", summary)
    control["verification"] = dict(summary, evidence=str((evidence / "verification.json").relative_to(ROOT)))
    control["activity"] = dict(phase="verify", status="passed" if passed else "failed", iteration=iteration)
    save(folder / "control.json", control)
    return dict(passed=bool(passed), scope='LocalVerifyOnly', overall009Passed=False,
                ledger=str((evidence/'009-execution-ledger.json').relative_to(ROOT)),
                evidence=control["verification"]["evidence"])


def step(action, phase):
    request, folder, control = context()
    verify_protected(request, control)
    if action == "preflight":
        feature = feature_path(request["feature"])
        if request["mode"] == "continue":
            freeze(request, folder, control)
            if request["feature"] == STATION and request["milestone"] == "M2":
                rows = control["task_baseline"]
                if any(not rows[k]["checked"] for k in M1):
                    raise ValueError("M1任务尚未全部完成，不能直接接续M2。")
        return {"ready": True}
    if action == "begin":
        if phase == "fix":
            if control.get("fix_count", 0) >= 2:
                raise ValueError("两轮自动修复已用尽；保留失败证据，需人工决定后续范围。")
            control["fix_count"] = control.get("fix_count", 0) + 1
        nonce = uuid.uuid4().hex
        current = dict(request_id=request["request_id"], phase=phase, nonce=nonce,
                       report=f"reports/{phase}-{nonce}.json", started=stamp())
        control["current"] = current
        control["activity"] = dict(phase=phase, status="running")
        save(folder / "control.json", control)
        return current
    if action == "accept":
        report = check_report(request, folder, control)
        if report["status"] == "blocked" or (phase not in ("implement", "fix") and report["status"] != "pass"):
            raise ValueError("阶段未通过；查看报告中的待澄清或阻断项。")
        feature = feature_path(request["feature"])
        required = {"specify": ["spec.md", "checklists/requirements.md"],
                    "plan": ["plan.md", "research.md", "data-model.md", "quickstart.md"],
                    "tasks": ["tasks.md"]}.get(phase, [])
        if any(not (feature / p).is_file() for p in required):
            raise ValueError("阶段报告通过，但必需产物缺失。")
        if phase == "tasks":
            freeze(request, folder, control)
        return {"accepted": True}
    if action == "verify":
        return run_verify(request, folder, control)
    if action == "assess":
        report = check_report(request, folder, control)
        if report["status"] == "blocked":
            raise ValueError("审查发现需要用户决定的阻断；不进入自动修复。")
        verification = control.get("verification", {})
        rows = task_rows(feature_path(request["feature"]) / "tasks.md")
        unchecked = [k for k in control["selected"] if not rows[k]["checked"]]
        current_source = source_digest()
        light = recipe010.final_gate(verification.get("passed") is True,
            verification.get('lightweight_credential'), verification.get('acceptance_parent'),
            verification.get('profile'), 'assess')
        passed = (light['passed'] and report["status"] == "pass"
                  and not unchecked and verification.get("source_digest") == current_source)
        control["assessment"] = dict(passed=passed, unchecked=unchecked, report=control["current"]["report"])
        save(folder / "control.json", control)
        return {"needs_fix": not passed}
    if action == "finish":
        v = control.get("verification", {})
        light = recipe010.final_gate(v.get('passed') is True, v.get('lightweight_credential'),
            v.get('acceptance_parent'), v.get('profile'), 'finish')
        if (not control.get("assessment", {}).get("passed") or not light['passed']
                or v.get("source_digest") != source_digest()):
            raise ValueError("尚未达到完成条件，不生成Done。")
        rows = task_rows(feature_path(request["feature"]) / "tasks.md")
        if any(not rows[k]["checked"] for k in control["selected"]):
            raise ValueError("仍有未完成任务。")
        report = dict(request_id=request["request_id"], feature=request["feature"],
                      milestone=request["milestone"], status="software-scope-complete",
                      verification=v["evidence"], review=control["assessment"]["report"],
                      selected_tasks=control["selected"], fix_count=control.get("fix_count", 0),
                      limitations="仅所选软件范围；非真实设备、算法精度或生产验收结论。", time=stamp())
        save(folder / "completion.json", report)
        return report
    raise ValueError("未知步骤。")


def codex_binary():
    found = shutil.which("codex.exe")
    if found:
        return found
    npm = Path(os.environ.get("APPDATA", "")) / "npm" / "node_modules" / "@openai" / "codex"
    matches = sorted(npm.glob("node_modules/@openai/codex-win32-*/vendor/*/bin/codex.exe"))
    if len(matches) != 1:
        raise ValueError("无法唯一定位Codex原生exe；检查本机Codex安装，不使用不明确的cmd转义。")
    return str(matches[0])


def check():
    from specify_cli.workflows.engine import WorkflowDefinition, WorkflowEngine
    definition = WorkflowDefinition.from_yaml(YAML)
    errors = WorkflowEngine(ROOT).validate(definition)
    if errors:
        raise ValueError("\n".join(errors))
    for name in ("specify", "pwsh", "dotnet"):
        if not shutil.which(name):
            raise ValueError("未找到开发工具：" + name)
    codex = codex_binary()
    for p in ("scripts/workflow/stages.md", "scripts/workflow/step.ps1",
              "scripts/workflow/prompt-stage.ps1", "scripts/workflow/prompt_stage.py",
              "scripts/workflow/session-bridge.ps1", "scripts/workflow/execution.py",
              "scripts/workflow/doctor.py", "scripts/workflow/doctor-accept.ps1",
              "scripts/workflow/resilience.py", "scripts/workflow/bounded_logs.py",
              "scripts/workflow/test_bridge.py", "scripts/workflow/verify_entry.py",
              "scripts/verify.ps1", "dev-parallel.ps1", "scripts/workflow/locks.py", "scripts/workflow/workspaces.py",
              ".specify/memory/constitution.md", ".specify/templates/spec-template.md",
              ".specify/templates/plan-template.md", ".specify/templates/tasks-template.md"):
        if not (ROOT / p).is_file():
            raise ValueError("缺少依赖：" + p)
    return dict(configuration="valid", workflow=str(YAML.relative_to(ROOT)),
                codex=codex, execution="NotRun", authentication="NotChecked")


@contextmanager
def workspace_lock(wait_seconds=0):
    with file_lock(WF / "runner.lock", workspace=ROOT, wait_seconds=wait_seconds):
        yield


def dispatch(args):
    if not args.workspace_idle:
        raise ValueError("请先停止其他开发会话写入，再加-WorkspaceIdle。检查配置可用-Check，不需此确认。")
    check()
    with workspace_lock():
        if args.command == "resume":
            if not re.fullmatch(r"[a-zA-Z0-9][a-zA-Z0-9_-]{0,79}", args.run_id):
                raise ValueError("run_id非法。")
            state = load(inside(WF / "runs" / args.run_id / "state.json", WF / "runs"))
            if state.get("status") not in ("paused", "failed"):
                raise ValueError("只恢复paused/failed运行；running须先确认旧进程退出，不改写状态冒充失败。")
            request_id = state.get("inputs", {}).get("request_id")
            # Spec Kit persists inputs separately in some versions.
            inputs_file = WF / "runs" / args.run_id / "inputs.json"
            if not request_id and inputs_file.is_file():
                saved_inputs = load(inputs_file)
                request_id = saved_inputs.get("inputs", saved_inputs).get("request_id")
            if not isinstance(request_id, str) or not re.fullmatch(r"[0-9a-f]{32}", request_id):
                raise ValueError("无法定位本项目Workflow请求。")
            folder = inside(WF / "requests" / request_id, WF / "requests")
            request = load(folder / "request.json")
            if (ROOT / "workspace.json").is_file():
                if load(ROOT / "workspace.json").get("feature") != request["feature"]:
                    raise ValueError("恢复的Feature与并行工作区预留范围不一致")
            verify_protected(request, load(folder / "control.json"))
            command = ["specify", "workflow", "resume", args.run_id]
        else:
            request_id = uuid.uuid4().hex
            feature = args.feature
            if (ROOT / "workspace.json").is_file():
                reserved = load(ROOT / "workspace.json").get("feature")
                if feature and feature != reserved:
                    raise ValueError("Feature与并行工作区预留范围不一致")
                feature = reserved
            if args.mode == "continue":
                feature = feature or STATION
                if not feature_path(feature).is_dir():
                    raise ValueError("接续功能目录不存在。")
            else:
                if not args.requirement.strip():
                    raise ValueError("新功能必须提供需求。")
                if not feature:
                    numbers = [int(p.name.split("-")[0]) for p in (ROOT / "specs").iterdir()
                               if p.is_dir() and re.fullmatch(r"\d+-[a-z0-9-]+", p.name)]
                    feature = f"{max(numbers, default=0) + 1:03d}-feature-{request_id[:8]}"
                if feature_path(feature).exists():
                    raise ValueError("新功能目录已存在，请用Continue而不是覆盖。")
            milestone = args.milestone if args.mode == "continue" and feature == STATION else "All"
            request = dict(request_id=request_id, mode=args.mode, feature=feature, milestone=milestone,
                           requirement=args.requirement, created=stamp(), fix_limit=2,
                           workspace_idle_confirmed=True)
            folder = inside(WF / "requests" / request_id, WF / "requests")
            control = dict(protected=protected_files(feature_path(feature)), fix_count=0)
            save(folder / "request.json", request)
            save(folder / "control.json", control)
            command = ["specify", "workflow", "run", str(YAML),
                       "--input", f"request_id={request_id}", "--input", f"mode={args.mode}"]
        env = os.environ.copy()
        env.update(GAODE_WORKFLOW_REQUEST=str(folder / "request.json"),
                   GAODE_WORKFLOW_PYTHON=sys.executable, PYTHONUTF8="1", PYTHONDONTWRITEBYTECODE="1",
                   SPECKIT_INTEGRATION_CODEX_EXECUTABLE=codex_binary(),
                   SPECKIT_INTEGRATION_CODEX_EXTRA_ARGS="--sandbox workspace-write --skip-git-repo-check")
        env.pop("SPECIFY_INIT_DIR", None)
        env.pop("SPECKIT_WORKFLOW_RUN_ID", None)
        print("Workflow请求：", folder.relative_to(ROOT), flush=True)
        with heartbeat(folder, "Workflow"):
            return subprocess.call(command, cwd=ROOT, env=env)


def status():
    result = []
    for state_file in sorted((WF / "runs").glob("*/state.json")) if (WF / "runs").exists() else []:
        item = load(state_file)
        row = {k: item.get(k) for k in ("run_id", "workflow_id", "status", "current_step_id", "error")}
        inputs = item.get("inputs", {})
        request_id = inputs.get("request_id") if isinstance(inputs, dict) else None
        inputs_file = state_file.parent / "inputs.json"
        if not request_id and inputs_file.is_file():
            try:
                saved = load(inputs_file)
                request_id = saved.get("inputs", saved).get("request_id")
            except (OSError, ValueError, AttributeError):
                request_id = None
        if isinstance(request_id, str):
            control_file = WF / "requests" / request_id / "control.json"
            if control_file.is_file():
                try:
                    activity = load(control_file).get("activity", {})
                    if activity.get("status") == "paused_retryable":
                        failure_kind = str(activity.get("failure_kind") or "retryable_service_failure")
                        row.update(status="paused_retryable",
                                   error=failure_kind + "; " + str(activity.get("next_action") or ""),
                                   failure_kind=failure_kind,
                                   session_id=activity.get("session_id"),
                                   paused_at=activity.get("paused_at"))
                except (OSError, ValueError, TypeError):
                    pass
        result.append(row)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=("check", "status", "start", "resume", "step"))
    parser.add_argument("--mode", choices=("new", "continue"), default="new")
    parser.add_argument("--requirement", default="")
    parser.add_argument("--feature")
    parser.add_argument("--milestone", choices=("M1", "M2", "All"), default="M1")
    parser.add_argument("--run-id")
    parser.add_argument("--workspace-idle", action="store_true")
    parser.add_argument("--action")
    parser.add_argument("--phase", default="review")
    args = parser.parse_args()
    try:
        if args.command in ("start", "resume"):
            return dispatch(args)
        result = check() if args.command == "check" else status() if args.command == "status" else step(args.action, args.phase)
        print(json.dumps(result, ensure_ascii=False))
        return 0
    except (OSError, ValueError, KeyError, TypeError) as exc:
        print(str(exc), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
