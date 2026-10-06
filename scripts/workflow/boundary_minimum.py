"""009's finite code-boundary entry; no device/process-flow acceptance."""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import sys
import uuid
import xml.etree.ElementTree as ET

import runner
import recipe_execution_010 as recipe010

ROOT = runner.ROOT
SCOPE = "BoundaryMinimum"
MANIFEST = ROOT / "scripts/workflow/009-boundary-minimum-cases.json"


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def source_identity():
    # The existing digest covers code/contracts/assertions/tools. Include page
    # consumers as evidence identity too; hashing does not replace AST checks.
    h = hashlib.sha256(runner.source_digest().encode())
    for path in sorted((ROOT / "frontend").rglob("*")):
        if path.is_file() and path.suffix.lower() in {".html", ".css"} and not {
                "node_modules", "dist", "bin", "obj"}.intersection(path.parts):
            h.update(path.relative_to(ROOT).as_posix().encode())
            h.update(path.read_bytes())
    return h.hexdigest()


def build_identity():
    paths = set()
    entries = [f"backend/tests/{suite}/bin/Debug/net10.0/{suite}.dll" for suite in runner.SUITES]
    entries += ["backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll",
                "VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll"]
    for name in entries:
        entry = ROOT / name
        if not entry.is_file():
            raise ValueError("RequiredBuildOutputMissing:" + name)
        paths.update(p for p in entry.parent.iterdir() if p.is_file() and p.suffix in {".dll", ".json"})
    files = {p.relative_to(ROOT).as_posix(): digest(p) for p in sorted(paths)}
    return hashlib.sha256(json.dumps(files, sort_keys=True).encode()).hexdigest(), files


def load_manifest():
    manifest = runner.load(MANIFEST)
    if manifest.get("schemaVersion") != "009-boundary-minimum-cases/1" or manifest.get("scope") != SCOPE:
        raise ValueError("BoundaryScopeInvalid")
    cases = manifest.get("cases", [])
    ids = [c["caseId"] for c in cases]
    if not ids or len(ids) != len(set(ids)) or any(c["scope"] != SCOPE for c in cases):
        raise ValueError("FixedCaseInventoryInvalid")
    if not manifest.get("requiredFiles"):
        raise ValueError("RequiredScannerInventoryMissing")
    for name in manifest["requiredFiles"]:
        if not runner.inside(ROOT / name).is_file():
            raise ValueError("RequiredScannerMissing:" + name)
    return manifest


def _main(credential, parent, profile):
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence", required=True, type=Path)
    args = parser.parse_args()
    evidence = runner.inside(args.evidence, ROOT / "artifacts/recipe-execution-008/009-isolation")
    if evidence.exists() and any(evidence.iterdir()):
        raise ValueError("EvidenceRootMustBeNew")
    manifest = load_manifest()  # fixed before any discovery or execution
    evidence.mkdir(parents=True, exist_ok=True)
    runner.save(evidence / "fixed-cases.json", manifest)
    context = dict(runId="boundary-min-" + uuid.uuid4().hex, sourceDigest=source_identity(),
                   manifestDigest=digest(MANIFEST), buildDigest="BuildNotExecuted",
                   startedAt=datetime.now(timezone.utc).isoformat())
    env = dict(os.environ, PYTHONUTF8="1", PYTHONDONTWRITEBYTECODE="1",
               GAODE_009_EVIDENCE_ROOT=str(evidence / "native"), GAODE_009_RUN_ID=context["runId"])
    commands, observed, errors = [], [], []

    def execute(label, command):
        code = runner.run_logged(command, evidence / (label + ".log"), env, timeout=900)
        commands.append(dict(label=label, command=command, exitCode=code))
        print(label + ": " + str(code), flush=True)
        if code:
            errors.append(label + ":ExitCode=" + str(code))
        return code == 0

    def row(case_id, discovered, executed, outcome, kinds):
        observed.append(dict(context, caseId=case_id, discovered=discovered,
                             executed=executed, outcome=outcome, evidenceKinds=kinds))

    builds = [("BUILD-BACKEND", "backend/Gaode.slnx"), ("BUILD-VIRTUAL-PLC", "VirtualPlc/VirtualPlc.csproj")]
    build_ok = []
    for case_id, project in builds:
        build_ok.append(execute(case_id, ["dotnet", "build", project, "--no-restore", "--nologo"]))
    if all(build_ok):
        context["buildDigest"], binaries = build_identity()
        runner.save(evidence / "build-manifest.json", dict(context, files=binaries))
    for (case_id, _), passed in zip(builds, build_ok, strict=True):
        row(case_id, True, True, "Passed" if passed else "Failed", ["build-log"])
    if all(build_ok):
        for suite in ["Gaode.Rules.Tests", "Gaode.Contracts.Tests"]:
            fixed = [c for c in manifest["cases"] if c.get("suite") == suite]
            # Explicit exact reviewed classes/methods, never discovered names.
            filters = sorted({c["method"].split("(")[0].rsplit(".", 1)[0]
                              if suite == "Gaode.Rules.Tests" else c["method"].split("(")[0]
                              for c in fixed})
            expression = "|".join("FullyQualifiedName=" + name if suite.endswith("Contracts.Tests")
                                  else "FullyQualifiedName~" + name for name in filters)
            project = f"backend/tests/{suite}/{suite}.csproj"
            command = ["dotnet", "test", project, "--no-build", "--no-restore", "--filter", expression]
            discovered_ok = execute(suite + "-discovery", command + ["--list-tests"])
            executed_ok = execute(suite, command + ["--logger", "trx;LogFileName=" + suite + ".trx",
                                                    "--results-directory", str(evidence)])
            try:
                discovery = {runner.test_identity(line.strip()) for line in
                             (evidence / (suite + "-discovery.log")).read_text(encoding="utf-8-sig").splitlines()
                             if line.strip().startswith("Gaode.")}
                trx = evidence / (suite + ".trx")
                if not runner.read_trx(trx):
                    errors.append(suite + ":IncompleteOrSkippedTrx")
                tree = ET.parse(trx)
                times = tree.find(".//{*}Times")
                if times is None or datetime.fromisoformat(times.attrib["start"].replace("Z", "+00:00")) < datetime.fromisoformat(context["startedAt"]):
                    raise ValueError("TrxPrecedesCurrentRun")
                actual = {}
                for item in tree.findall(".//{*}UnitTestResult"):
                    actual.setdefault(runner.test_identity(item.attrib["testName"]), []).append(item.attrib["outcome"])
                expected = {(c["method"], c.get("dataRow")) for c in fixed}
                if discovery != expected or set(actual) != expected:
                    errors.append(suite + ":FixedDiscoveryOrExecutionMismatch:" + str(sorted(expected ^ discovery)))
                kinds = ["trx"]
                if suite == "Gaode.Rules.Tests":
                    proof = runner.load(evidence / "native/csharp-boundary.json")
                    valid_source = (proof.get("result") == "Passed" and not proof.get("violations")
                                    and proof.get("runId") == context["runId"] and bool(proof.get("files")))
                    for item in proof.get("files", []):
                        path = runner.inside(ROOT / item["path"])
                        valid_source = valid_source and path.is_file() and digest(path).upper() == item["sha256"]
                    if not valid_source:
                        errors.append("CSharpSourceEvidenceMissingStaleOrRejected")
                    else:
                        kinds.append("csharp-source")
                for case in fixed:
                    identity = (case["method"], case.get("dataRow"))
                    outcomes = actual.get(identity, [])
                    row(case["caseId"], discovered_ok and identity in discovery,
                        executed_ok and len(outcomes) == 1, outcomes[0] if len(outcomes) == 1 else "MissingOrDuplicate", kinds)
            except (OSError, ValueError, ET.ParseError, KeyError) as exc:
                errors.append(suite + ":" + str(exc))

        scripts = evidence / "script-boundary.json"
        script_ok = execute("script-boundary", [sys.executable, "-B", "scripts/check-009-script-boundary.py",
                            "--run-id", context["runId"], "--output", str(scripts)])
        try:
            report = runner.load(scripts)
            inventory_path = ROOT / "backend/tests/Gaode.Rules.Tests/Architecture/009-boundary-inventory.json"
            inventory = runner.load(inventory_path)
            h = hashlib.sha256()
            for name in sorted({c["path"] for c in inventory["files"]} | {"scripts/architecture/009-script-boundary-cases.json"}):
                path = ROOT / name
                if path.is_file():
                    h.update(name.encode()); h.update(path.read_bytes())
            valid = (script_ok and report["result"] == "Passed" and report["runId"] == context["runId"]
                     and report["sourceDigest"] == h.hexdigest() and report["inventoryDigest"] == digest(inventory_path)
                     and report["scope"] == "RepositoryAndCases" and not report["errors"] and not report["violations"]
                     and report["parserVersions"] == inventory["scriptParserVersions"])
            for case in report["cases"]:
                row(case["caseId"], valid, valid, case["outcome"], ["ast-case"])
            scanned = {f["path"] for f in report["files"] if f["parsed"]}
            for suffix, extensions in [("JS", {".js", ".cjs", ".mjs", ".ts"}), ("PY", {".py"}), ("PS", {".ps1", ".psm1"})]:
                needed = {f["path"] for f in inventory["files"] if Path(f["path"]).suffix.lower() in extensions}
                complete = valid and bool(needed) and needed.issubset(scanned)
                row("SCRIPT-SOURCE-" + suffix, complete, complete, "Passed" if complete else "Rejected", ["ast-source"])
            row("SCRIPT-INVENTORY", valid, valid, "Passed" if valid else "Rejected", ["ast-source"])
        except (OSError, ValueError, KeyError) as exc:
            errors.append("ScriptReport:" + str(exc))

        selfcheck = evidence / "ledger-selfcheck.json"
        checked = execute("ledger-selfcheck", [sys.executable, "-B", "scripts/workflow/test_verify.py", "--ledger-selfcheck",
                          "--output", str(selfcheck), "--run-id", context["runId"], "--source-digest", context["sourceDigest"],
                          "--manifest-digest", context["manifestDigest"]])
        try:
            report = runner.load(selfcheck)
            valid = checked and report["result"] == "Passed" and all(report[k] == context[k] for k in ("runId", "sourceDigest", "manifestDigest"))
            for case in report["cases"]:
                row(case["caseId"], valid, valid, case["outcome"], ["runner-selfcheck"])
        except (OSError, ValueError, KeyError) as exc:
            errors.append("LedgerSelfcheck:" + str(exc))

    unchanged = source_identity() == context["sourceDigest"] and digest(MANIFEST) == context["manifestDigest"]
    if all(build_ok):
        unchanged = unchanged and build_identity()[0] == context["buildDigest"]
    if not unchanged:
        errors.append("SourceManifestOrBuildChangedDuringRun")
    ledger = runner.validate_required_ledger(manifest, observed, context, SCOPE)
    lightweight = recipe010.final_gate(not errors and ledger['result'] == 'Passed', credential, parent, profile, 'boundary_minimum')
    if not lightweight['passed']:
        errors.extend(lightweight['lightweight'].get('errors', ['LightweightRejected']))
        ledger['result'] = 'Rejected'
    runner.save(evidence / "execution-ledger.json", dict(context, **ledger, lightweightCredential=credential, acceptanceParent=parent))
    result = dict(context, lightweightCredential=credential, acceptanceParent=parent, scope=SCOPE, scopeVersion=manifest["scopeVersion"],
                  result="Passed" if not errors and ledger["result"] == "Passed" else "Rejected",
                  manualReviewRequired=True, completeDynamic009Passed=False,
                  finishedAt=datetime.now(timezone.utc).isoformat(), commands=commands, errors=errors,
                  requiredCount=ledger["requiredCount"], observedCount=ledger["observedCount"])
    result["attachments"] = {p.relative_to(evidence).as_posix(): digest(p) for p in sorted(evidence.rglob("*")) if p.is_file()}
    runner.save(evidence / "result.json", result)
    print(json.dumps({k: result[k] for k in ("scope", "result", "requiredCount", "observedCount", "errors")}, ensure_ascii=False), flush=True)
    return 0 if result["result"] == "Passed" else 1


def main():
    parent = 'boundary-' + uuid.uuid4().hex
    checked = recipe010.run_with_lightweight(parent, 'BoundaryMinimum',
        lambda credential: dict(passed=_main(credential, parent, 'BoundaryMinimum') == 0), point='boundary_minimum')
    return 0 if checked['passed'] else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, KeyError) as exc:
        print("BoundaryMinimum rejected: " + str(exc), file=sys.stderr)
        sys.exit(2)
