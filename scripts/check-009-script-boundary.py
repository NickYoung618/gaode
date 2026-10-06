"""Finite 009 script boundary. AST content, inventory and evidence are separate gates."""
from __future__ import annotations
import argparse
import ast
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RULE_VERSION = "009-script-boundary/3"
RAW = {"alarmbits", "alarmseverity", "protocolstatus", "inspectionstatus", "zresetstatus",
       "flipstatus", "palletlockstatus", "plcsystemfault", "reliablefeedback", "sortingackcleared",
       "documentnumber", "pduoffset", "registeraddress", "rawwords", "rawbytes", "positionevidence",
       "plcwriteaudit", "plcaudit", "plcchanges", "plcstate"}
# Frozen finite rule vocabulary is data, not a business assertion or an exemption.
HANDSHAKE_FACTS = frozenset(json.loads((ROOT / "scripts/architecture/009-script-boundary-cases.json")
                                    .read_text(encoding="utf-8"))["internalHandshakeIdentities"])
DEVICE_FIELDS = {'schemaVersion','reliability','connection','connectionEpoch','operatingMode','readiness',
    'safetyAssessment','clamp','motionAvailability','acquisitionReadiness','manualArea','manualHandling',
    'position','face','alarms','reasonCodes','executionOrigin','observationId','sampleStartedUtc',
    'sampleEndedUtc','diagnosticEvidenceReference','axisObservations'}
DIAGNOSTIC_FIELDS = {'schemaVersion','safetyAssessment','reasonCodes','stopStage','disposition',
    'semanticObservation','executionOrigin','diagnosticEvidenceReference','connectionEpoch',
    'observedAtUtc','recordNature','rawAvailability'}
COMPLETION_FIELDS = {'finalOutcome','receiptValidity','canContinue','approvalValid'}


def check_python(item):
    errors, violations, dependencies = [], [], []
    try:
        tree = ast.parse(item["source"], filename=item["path"])
    except SyntaxError as exc:
        return dict(parsed=False, parserVersion=sys.version.split()[0], violations=[],
                    errors=[dict(code="PARSE", path=item["path"], line=exc.lineno, message=str(exc))], dependencies=[])
    aliases, seen, schemas = set(), set(), {}
    functions={n.name:n for n in ast.walk(tree) if isinstance(n,(ast.FunctionDef,ast.AsyncFunctionDef))}
    rebound={n.id for n in ast.walk(tree) if isinstance(n,ast.Name) and isinstance(n.ctx,ast.Store)} | set(functions)
    rebound.update(a.asname or a.name.split('.')[0] for n in ast.walk(tree) if isinstance(n,(ast.Import,ast.ImportFrom)) for a in n.names)
    rebound.update(n.arg for n in ast.walk(tree) if isinstance(n,ast.arg))
    def field(node):
        if isinstance(node, ast.Attribute): return node.attr
        if isinstance(node, ast.Subscript) and isinstance(node.slice, ast.Constant): return str(node.slice.value)
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == 'prop' and len(node.args)>1 and isinstance(node.args[1], ast.Constant): return str(node.args[1].value)
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute) and node.func.attr == 'get' and node.args and isinstance(node.args[0], ast.Constant): return str(node.args[0].value)
        return ""
    def owner(node):
        if isinstance(node, (ast.Attribute, ast.Subscript)): return node.value
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id=='prop' and node.args: return node.args[0]
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute) and node.func.attr == 'get': return node.func.value
    def schema(node):
        if isinstance(node, ast.Name): return schemas.get(node.id)
        key=field(node)
        if key in {'plc','semanticObservation'}: return 'device-semantics/1'
        if key=='startupDiagnostic': return 'startup-diagnostic/1'
    def merge_schema(name,kind):
        old=schemas.get(name)
        if old in {kind,'ambiguous'}: return False
        schemas[name]='ambiguous' if old else kind
        return True
    def raw(node):
        if node is None: return False
        if isinstance(node, ast.Name) and node.id in aliases: return True
        if field(node).lower() in RAW: return True
        if isinstance(node, ast.Call) and any(isinstance(a, ast.Constant) and isinstance(a.value, str) and a.value.lower() in RAW for a in node.args): return True
        return any(raw(child) for child in ast.iter_child_nodes(node))
    def add_names(node):
        old = len(aliases)
        for n in ast.walk(node):
            if isinstance(n, ast.Name): aliases.add(n.id)
        return len(aliases) != old
    changed = True
    while changed:
        changed = False
        for n in ast.walk(tree):
            if isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id in functions:
                target=functions[n.func.id]
                for argument,parameter in zip(n.args,target.args.args):
                    if raw(argument) and parameter.arg not in aliases: aliases.add(parameter.arg); changed=True
                    if schema(argument): changed=merge_schema(parameter.arg,schema(argument)) or changed
            if isinstance(n, (ast.Assign, ast.AnnAssign, ast.NamedExpr)) and schema(n.value):
                for target in n.targets if isinstance(n,ast.Assign) else [n.target]:
                    if isinstance(target,ast.Name): changed=merge_schema(target.id,schema(n.value)) or changed
            if isinstance(n, (ast.Assign, ast.AnnAssign, ast.NamedExpr)) and raw(n.value):
                for target in n.targets if isinstance(n, ast.Assign) else [n.target]: changed = add_names(target) or changed
            elif isinstance(n, ast.comprehension) and raw(n.iter): changed = add_names(n.target) or changed
            elif isinstance(n, (ast.For, ast.AsyncFor)) and raw(n.iter): changed = add_names(n.target) or changed
            elif isinstance(n, (ast.FunctionDef, ast.AsyncFunctionDef)) and any(raw(x.value) for x in ast.walk(n) if isinstance(x, ast.Return)):
                if n.name not in aliases: aliases.add(n.name); changed = True
    def reject(rule, node, message):
        key = (rule, node.lineno, node.col_offset)
        if key not in seen:
            seen.add(key); violations.append(dict(ruleId=rule, path=item["path"], line=node.lineno,
                column=node.col_offset+1, sourcePath=item["path"], message=message))
    communication = item["role"] in {"communication-probe", "communication-device"}
    for n in ast.walk(tree):
        if isinstance(n, (ast.Import, ast.ImportFrom)):
            dependencies.extend([a.name for a in n.names] if isinstance(n, ast.Import) else ["." * n.level + (n.module or "")])
        if communication:
            if item['role']=='communication-probe' and field(n) in COMPLETION_FIELDS:
                reject('A10',n,'Communication probe contains a business completion/approval assertion.')
            continue
        if isinstance(n, ast.Constant) and isinstance(n.value, str) and n.value in HANDSHAKE_FACTS:
            reject("A08", n, "Known internal handshake identity in protected business assertion.")
        input_schema=schema(owner(n))
        if input_schema=='ambiguous': reject('A09',n,'Conflicting device schema sources require an explicit boundary.')
        if input_schema and field(n) and field(n) not in (DEVICE_FIELDS if input_schema=='device-semantics/1' else DIAGNOSTIC_FIELDS) and field(n).lower() not in RAW:
            reject('A10',n,'Unregistered field in '+input_schema+': '+field(n))
        if input_schema and isinstance(n,ast.Subscript) and not isinstance(n.slice,ast.Constant):
            reject('A09',n,'Dynamic key on a protected device/diagnostic object.')
        if field(n).lower() in RAW: reject("A08", n, "Raw protocol field accessed in protected script.")
        if isinstance(n, (ast.Compare, ast.BinOp, ast.BoolOp)) and raw(n): reject("A08", n, "Raw value/alias controls a business assertion.")
        if isinstance(n, ast.Subscript) and raw(n.value) and not isinstance(n.slice, ast.Constant): reject("A09", n, "Dynamic raw property access unsupported.")
        if isinstance(n, ast.Call):
            name = ast.unparse(n.func)
            if name in {'importlib.import_module','__import__'}:
                reject('A10',n,'Dynamic import is outside the finite local module closure.')
            if name in {'eval','exec','__import__'} or name in {'getattr','setattr'} and any(raw(a) for a in n.args):
                reject('A09', n, 'Dynamic evaluation/reflection is outside the finite business assertion boundary.')
            if any(raw(a) for a in n.args) and name not in {"print", "json.dumps", "prop", "next", "len"}:
                reject("A09", n, "Raw object escapes through helper/call.")
            type_predicate = (name == 'isinstance' and len(n.args) == 2 and not n.keywords and
                isinstance(n.args[1],ast.Name) and n.args[1].id == 'dict' and not {'isinstance','dict'}.intersection(rebound))
            if any(schema(a) for a in n.args) and name not in functions and name not in {'print','json.dumps','prop'} and not type_predicate:
                reject('A09',n,'Semantic device object escapes to an unmodelled helper.')
            if any(isinstance(a, ast.Constant) and isinstance(a.value, str) and a.value.lower() in RAW for a in n.args):
                reject("A08", n, "Raw field read through property helper.")
    return dict(parsed=True, parserVersion=sys.version.split()[0], violations=violations, errors=errors, dependencies=dependencies)


def parse_batch(language, items):
    if language == "python": return [check_python(x) for x in items]
    command = ["node", str(ROOT / "scripts/architecture/check-009-js.cjs")] if language == "javascript" else [
        "pwsh", "-NoProfile", "-File", str(ROOT / "scripts/architecture/check-009-powershell.ps1")]
    proc = subprocess.run(command, input=json.dumps(items), text=True, encoding="utf-8",
                          capture_output=True, cwd=ROOT, timeout=60)
    if proc.returncode: raise ValueError(f"ParserFailed:{language}:{proc.stderr}")
    values = json.loads(proc.stdout)
    if not isinstance(values, list) or len(values) != len(items): raise ValueError("ParserResultCountMismatch")
    return values


def language(path):
    suffix = Path(path).suffix.lower()
    return "python" if suffix == ".py" else "powershell" if suffix in {".ps1", ".psm1"} else "javascript" if suffix in {".js", ".cjs", ".mjs", ".ts"} else None


def inventory_errors(entries, discovered, communication_paths):
    registered = {x["path"]: x["role"] for x in entries}
    failures = [dict(code="A10", path=p, message="Unclassified first-party source/helper")
                for p in sorted(set(discovered) - registered.keys())]
    failures += [dict(code="A10", path=p, message="Communication probe role was not explicitly reviewed")
                 for p, role in registered.items() if role == "communication-probe" and p not in communication_paths]
    return failures


def parser_error(language_name, actual, expected):
    if not actual or actual != expected:
        return dict(code='PARSER-VERSION', path=language_name, message=f'Required parser {expected}, observed {actual}')


def dependency_errors(item, dependencies, registered):
    failures = []
    base = ROOT / item["path"]
    for ref in dependencies:
        if not ref: continue
        if language(base) == "python":
            if ref.split('.')[0] in sys.stdlib_module_names: continue
            relative=ref.lstrip('.').replace('.','/')
            names = [relative+'.py', relative+'/__init__.py', ref+'.py']
        else:
            if ref.startswith('node:') or ref in {'fs','path','os','crypto','http','https','url','assert','child_process','module'}: continue
            if not ref.startswith(('.', '/', 'E:', 'C:')) and '/' in ref and not Path(ref).suffix:
                continue  # External package identity; package lock is part of the source digest.
            names = [ref, ref+'.js', ref+'.ts', ref+'.py']
        candidates = [p for name in names for p in (base.parent/name, ROOT/name, ROOT/'scripts'/name)]
        resolved = [p.resolve() for p in candidates if p.is_file()]
        for p in candidates:
            if p.is_dir() and 'node_modules' in p.parts and (p/'package.json').is_file():
                resolved.append((p/'package.json').resolve())
        for p in resolved:
            if p.is_relative_to(ROOT) and not {'node_modules','bin','obj','dist'}.intersection(p.parts):
                relative = p.relative_to(ROOT).as_posix()
                if relative not in registered:
                    failures.append(dict(code='A10',path=item['path'],message='Local helper not classified: '+relative))
        if not resolved and ref.startswith('.'):
            failures.append(dict(code='A10',path=item['path'],message='Unresolved local helper: '+ref))
    return failures


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cases-only", action="store_true", help="Checker self-test subset, never a product gate pass")
    args = parser.parse_args()
    inventory_path = ROOT / "backend/tests/Gaode.Rules.Tests/Architecture/009-boundary-inventory.json"
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    cases_path = ROOT / "scripts/architecture/009-script-boundary-cases.json"
    cases = json.loads(cases_path.read_text(encoding="utf-8"))["cases"]
    files, violations, errors, outcomes, versions = [], [], [], [], {}
    registered = {x["path"] for x in inventory["files"]}
    for lang in ("javascript", "python", "powershell"):
        selected = [x for x in cases if x["language"] == lang]
        for case, result in zip(selected, parse_batch(lang, selected), strict=True):
            rules = {v["ruleId"] for v in result["violations"]} | {v["code"] for v in result["errors"]}
            passed = case["expectedRule"] in rules if case["expectedRule"] else not rules and result["parsed"]
            outcomes.append(dict(caseId=case["caseId"], kind=case["kind"], expectedRule=case["expectedRule"],
                                 observedRule=sorted(rules), outcome="Passed" if passed else "Failed"))
            versions[lang] = result["parserVersion"]
            version_error=parser_error(lang,result['parserVersion'],inventory['scriptParserVersions'][lang])
            if version_error: errors.append(version_error)
        if args.cases_only: continue
        selected = [x for x in inventory["files"] if language(x["path"]) == lang]
        items = [dict(path=x["path"], role=x["role"], source=(ROOT/x["path"]).read_text(encoding="utf-8-sig")) for x in selected]
        for item, result in zip(items, parse_batch(lang, items), strict=True):
            files.append(dict(path=item["path"], role=item["role"], parsed=result["parsed"]))
            violations.extend(result["violations"]); errors.extend(result["errors"])
            errors.extend(dependency_errors(item, result["dependencies"], registered))
    unclassified = []
    for base in inventory["discoveryRoots"]:
        for p in (ROOT/base).rglob("*"):
            if p.is_file() and language(p) and not set(p.parts).intersection(inventory["excludedGeneratedSegments"]):
                rel = p.relative_to(ROOT).as_posix()
                if rel not in registered: unclassified.append(rel)
    unclassified.extend(p.relative_to(ROOT).as_posix() for p in ROOT.iterdir() if p.is_file() and language(p) and p.name not in registered)
    if not args.cases_only:
        errors.extend(inventory_errors(inventory['files'], unclassified, inventory.get('communicationProbePaths', [])))
    # These negatives exercise the same inventory classifier as the formal source path.
    for case_id, entries, discovered in [
        ('N15-script-unclassified/new-helper', [], ['scripts/new-business-helper.py']),
        ('N15-script-unclassified/role-change', [{'path':'scripts/business.py','role':'communication-probe'}], ['scripts/business.py'])]:
        rejected = inventory_errors(entries, discovered, [])
        outcomes.append(dict(caseId=case_id,kind='negative',expectedRule='A10',observedRule=[x['code'] for x in rejected],
                             outcome='Passed' if any(x['code']=='A10' for x in rejected) else 'Failed'))
    for suffix,actual in [('missing',None),('wrong-version','0.0.0')]:
        failure=parser_error('javascript',actual,inventory['scriptParserVersions']['javascript'])
        outcomes.append(dict(caseId='N16-script-parser/'+suffix,kind='negative',expectedRule='PARSER-VERSION',
            observedRule=[failure['code']] if failure else [],outcome='Passed' if failure else 'Failed'))
    digest = hashlib.sha256()
    for name in sorted(registered | {str(cases_path.relative_to(ROOT)).replace('\\','/')}):
        p = ROOT/name
        if p.is_file(): digest.update(name.encode()); digest.update(p.read_bytes())
    failed = violations or errors or any(x["outcome"] != "Passed" for x in outcomes)
    report = dict(schemaVersion="009-script-boundary/1", runId=args.run_id, ruleSetVersion=RULE_VERSION,
        parserVersions=versions, sourceDigest=digest.hexdigest(), inventoryDigest=hashlib.sha256(inventory_path.read_bytes()).hexdigest(),
        scope="CheckerCasesOnly" if args.cases_only else "RepositoryAndCases", files=files, cases=outcomes,
        violations=violations, errors=errors, result="Rejected" if failed else "Passed")
    output = args.output.resolve()
    if not output.is_relative_to(ROOT/"artifacts/recipe-execution-008/009-isolation"):
        raise ValueError("EvidenceOutsideControlledRoot")
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("x", encoding="utf-8") as stream: json.dump(report, stream, ensure_ascii=False, indent=2)
    print(json.dumps(dict(result=report["result"], scope=report["scope"], cases=len(outcomes), violations=len(violations), errors=len(errors))))
    return 1 if failed else 0


if __name__ == "__main__":
    try: sys.exit(main())
    except (ValueError, OSError, subprocess.TimeoutExpired) as exc:
        print(f"009 script gate failed: {exc}", file=sys.stderr); sys.exit(2)
