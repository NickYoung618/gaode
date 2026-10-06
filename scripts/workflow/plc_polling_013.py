"""Fixed 013 measurement stages. Before is data collection, never an acceptance bypass."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import datetime
import uuid

ROOT = Path(__file__).resolve().parents[2]
METHOD = "Gaode.Integration.Tests.Station01.ThreeStageMainFlowIntegrationTests.CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite"
DIAGNOSTIC_PREFLIGHT = {"I-DIAG-01", "I-DIAG-02", "I-DIAG-03", "I-TIME-02", "I-TIME-03"}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def roots():
    attempt = Path(os.environ["GAODE_013_ATTEMPT_ROOT"]).resolve(strict=True)
    baseline = Path(os.environ["GAODE_013_BASELINE_ROOT"]).resolve(strict=True)
    after = Path(os.environ["GAODE_013_AFTER_ROOT"]).resolve(strict=True)
    manifest = json.loads((attempt / "baseline-manifest.json").read_text(encoding="utf-8-sig"))
    if baseline != Path(manifest["B"]) or after != Path(manifest["A"]) or after != ROOT:
        raise ValueError("013SourceIdentityMismatch")
    return attempt, baseline, after


def command(source, argv, log, env=None):
    log.parent.mkdir(parents=True, exist_ok=True)
    with log.open("w", encoding="utf-8") as output:
        output.write(json.dumps(dict(cwd=str(source), argv=argv)) + "\n")
        output.flush()
        completed = subprocess.run(argv, cwd=source, env=env, stdout=output,
                                   stderr=subprocess.STDOUT, timeout=900)
    if completed.returncode:
        raise RuntimeError("CommandFailed:" + str(log))


def build_before():
    attempt, baseline, _ = roots()
    folder = attempt / "before"
    if (folder / "build-manifest.json").exists():
        raise ValueError("BeforeBuildAlreadyRecorded")
    project = "backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj"
    command(baseline, ["dotnet", "build", project, "-c", "Debug", "-p:RestoreLockedMode=true"], folder / "build.log")
    products = [baseline / p for p in (
        "backend/tests/Gaode.Integration.Tests/bin/Debug/net10.0/Gaode.Integration.Tests.dll",
        "backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll",
        "backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll",
        "VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll")]
    files = {str(p.relative_to(baseline)): sha(p) for directory in {p.parent for p in products}
             for p in directory.iterdir() if p.is_file()}
    save(folder / "build-manifest.json", dict(sourceRoot=str(baseline), configuration="Debug", products=files,
         requiredProducts=[str(p) for p in products], timestamp=datetime.datetime.now(datetime.timezone.utc).isoformat()))


def measure(side):
    attempt, baseline, after = roots()
    source = baseline if side == "before" else after
    folder = attempt / side
    if (folder / "representative.trx").exists() or (folder / "full-run").exists():
        raise ValueError("MeasurementAlreadyAttempted:" + side)
    build = json.loads((folder / "build-manifest.json").read_text(encoding="utf-8"))
    if build["sourceRoot"] != str(source):
        raise ValueError("013OwnBuildRequired")
    for relative, digest in build["products"].items():
        if sha(source / relative) != digest:
            raise ValueError("013BuildChanged:" + relative)
    env = os.environ.copy()
    env.update(GAODE_013_SOURCE_ROOT=str(source), GAODE_011_FIXTURE=str(attempt / "inputs" / side / "run-2.json"),
               GAODE_011_FULLRUN_ROOT=str(folder / "full-run"), GAODE_011_PYTHON=sys.executable,
               PYTHONDONTWRITEBYTECODE="1", PYTHONUTF8="1")
    command(source, ["dotnet", "test", "backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj",
            "--no-build", "--no-restore", "--filter", "FullyQualifiedName=" + METHOD,
            "--logger", "trx;LogFileName=representative.trx", "--results-directory", str(folder)], folder / "representative.log", env)
    save(folder / "measurement-stage.json", dict(side=side, scope="MeasurementOnly", acceptancePassed=False,
         note="Original business assertions required; no after-only thresholds or final ledger prerequisite",
         sourceRoot=str(source), report=str(folder / "representative.trx")))


def current_component_identity(parent):
    import recipe_execution_010 as gate
    attempt, _, after = roots()
    fixed_path = ROOT / "scripts/workflow/013-required-cases.json"
    source = gate.source_files()
    build = json.loads((attempt / "after/build-manifest.json").read_text(encoding="utf-8"))
    if source != build["sourceFiles"]: raise ValueError("AfterSourceChangedSinceBuild")
    for relative, digest in build["products"].items():
        if sha(after / relative) != digest: raise ValueError("AfterBuildChanged:" + relative)
    return dict(parentAttemptId=parent, profile="PlcPolling013", sourceDigest=gate.value_digest(source),
        buildDigest=gate.value_digest(build["products"]), manifestDigest=sha(fixed_path),
        inputDigest=gate.input_digest(source), checkerDigest=sha(Path(__file__)))


def diagnostic_preflight(parent):
    """Five fixed diagnostic/deadline component rows only; this stage never grants acceptance or an L credential."""
    import recipe_execution_010 as gate
    attempt, _, _ = roots()
    if not parent or not parent.startswith("standalone-") or not parent.endswith("-verify-1"):
        raise ValueError("ExplicitFinalParentRequired")
    context = dict(current_component_identity(parent), verificationAttemptId=uuid.uuid4().hex, startedAt=gate.now())
    folder = attempt / "diagnostic-preflight"
    folder.mkdir(parents=True, exist_ok=False)
    fixed = json.loads((ROOT / "scripts/workflow/013-required-cases.json").read_text(encoding="utf-8-sig"))
    cases = [c for c in fixed["cases"] if c["caseId"] in DIAGNOSTIC_PREFLIGHT]
    if {c["caseId"] for c in cases} != DIAGNOSTIC_PREFLIGHT: raise ValueError("DiagnosticPreflightCasesMissing")
    save(folder / "context.json", context)
    env = dict(os.environ, PYTHONUTF8="1", PYTHONDONTWRITEBYTECODE="1",
        GAODE_009_EVIDENCE_ROOT=str(folder / "native"), GAODE_TEST_ROOT=str(folder / "test-stores"),
        GAODE_013_MEASUREMENT_ROOT=str(folder / "timing"))
    command_result = gate.execute_dotnet(folder, context, cases, env, "I-DIAG", 300)
    rows, errors = gate.trx_rows(folder / command_result["report"], folder / command_result["discovery"], cases, context)
    if command_result["exitCode"]: errors.append("DiagnosticPreflightExecutionFailed")
    qualified = gate.validate_rows(cases, rows, context, errors)
    reports = {str(p.relative_to(folder)): sha(p) for p in folder.rglob("*") if p.is_file()}
    save(folder / "reference.json", dict(command=command_result, reports=reports, rows=rows,
        qualification=qualified, acceptancePassed=False, scope="FiveFixedDiagnosticAndDeadlineComponentsOnly"))
    if current_component_identity(parent) != {k: context[k] for k in current_component_identity(parent)}:
        raise ValueError("IdentityChangedDuringDiagnosticPreflight")
    if qualified["result"] != "Passed": raise ValueError("DiagnosticPreflightFailed:" + str(folder))


def run_profile(parent, credential):
    import recipe_execution_010 as gate
    from plc_polling_013_compare import compare, closure_decision
    attempt, _, after = roots()
    fixed_path = ROOT / "scripts/workflow/013-required-cases.json"
    fixed = json.loads(fixed_path.read_text(encoding="utf-8-sig"))
    folder = attempt / "final" / parent
    folder.mkdir(parents=True, exist_ok=False)
    source = gate.source_files()
    build = json.loads((attempt / "after/build-manifest.json").read_text(encoding="utf-8"))
    if source != build["sourceFiles"]: raise ValueError("AfterSourceChangedSinceBuild")
    if not gate.final_gate(True, credential, parent, "PlcPolling013", "013-admission")["passed"]:
        raise ValueError("CurrentCompleteLightweightCredentialRequired")
    identity = current_component_identity(parent)
    preflight = attempt / "diagnostic-preflight"
    context = (json.loads((preflight / "context.json").read_text(encoding="utf-8")) if preflight.exists()
        else dict(identity, verificationAttemptId=uuid.uuid4().hex, startedAt=gate.now()))
    if any(context.get(k) != v for k, v in identity.items()): raise ValueError("DiagnosticPreflightIdentityChanged")
    save(folder / "context.json", context); save(folder / "fixed-cases.json", fixed)
    env = dict(os.environ, PYTHONUTF8="1", PYTHONDONTWRITEBYTECODE="1", GAODE_009_EVIDENCE_ROOT=str(folder / "native"),
               GAODE_TEST_ROOT=str(folder / "test-stores"))
    cases = [c for c in fixed["cases"] if c["kind"] == "dotnet"]
    rows=[]; errors=[]; commands=[]
    reused = set()
    if preflight.exists():
        reference = json.loads((preflight / "reference.json").read_text(encoding="utf-8"))
        for relative, digest in reference["reports"].items():
            path = (preflight / relative).resolve()
            if not path.is_relative_to(preflight) or sha(path) != digest: raise ValueError("DiagnosticPreflightReportChanged:" + relative)
        original = reference["command"]
        selected = [c for c in cases if c["caseId"] in DIAGNOSTIC_PREFLIGHT]
        if original["context"] != gate.identity(context) or set(original["caseIds"]) != DIAGNOSTIC_PREFLIGHT or original["exitCode"]:
            raise ValueError("DiagnosticPreflightCommandInvalid")
        actual, parse_errors = gate.trx_rows(preflight / original["report"], preflight / original["discovery"], selected, context)
        if gate.validate_rows(selected, actual, context, parse_errors)["result"] != "Passed":
            raise ValueError("DiagnosticPreflightRawReportsRejected")
        rows.extend(actual); reused = DIAGNOSTIC_PREFLIGHT
        save(folder / "preflight-reference.json", dict(folder=str(preflight), referenceSha256=sha(preflight / "reference.json"),
            context=context, caseIds=sorted(reused), note="Original context, times and execution IDs retained; complete L still required"))
    for suite in ("Gaode.Rules.Tests", "Gaode.Communication.Tests", "Gaode.Contracts.Tests"):
        selected=[c for c in cases if c["suite"]==suite and c["caseId"] not in reused]
        if not selected: raise ValueError("RequiredSuiteEmpty:"+suite)
        command_result=gate.execute_dotnet(folder, context, selected, env, suite, 900)
        commands.append(command_result)
        if command_result["exitCode"]: errors.append("ComponentExecutionFailed:"+suite)
        try:
            actual, parse_errors=gate.trx_rows(folder / command_result["report"], folder / command_result["discovery"], selected, context)
            rows.extend(actual); errors.extend(parse_errors)
        except Exception as exc:errors.append(type(exc).__name__+":"+str(exc))
    qualified=gate.validate_rows(cases,rows,context,errors)
    save(folder / "commands.json",commands); save(folder / "component-ledger.json",dict(qualified,context=context,rows=rows))
    # N3 has no self dependency: the complete real component sub-ledger is its positive control.
    if qualified["result"]!="Passed":
        result=dict(passed=False,scope="PlcPolling013",classification="BusinessProtectionOrRequiredComponentFailed",components=qualified,evidence=str(folder))
        save(folder / "result.json", result); return result
    removed=next(c["caseId"] for c in cases if c["obligation"]=="I-GAP")
    rejected=gate.validate_rows(cases,[r for r in rows if r["caseId"]!=removed],context)
    negative=dict(caseId="N3",positive=qualified,removedCase=removed,rejected=rejected,
                  realLedger=str(folder / "component-ledger.json"),realLedgerSha256=sha(folder / "component-ledger.json"))
    save(folder / "N3.json",negative)
    if rejected["result"]!="Rejected" or rejected["errors"] != ["MissingOrDuplicateExecution:"+removed]:
        raise ValueError("N3DidNotRejectExactlyTheRemovedRequiredRow")
    measure("after")
    comparison=compare(attempt)
    save(folder / "compare.json",comparison)
    if source != gate.source_files():raise ValueError("SourceChangedDuring013Verification")
    light = gate.final_gate(True, credential, parent, "PlcPolling013", "013-final")
    decision = closure_decision(comparison, qualified, light["lightweight"], negative,
        [] if light["passed"] else ["FinalLightweightCredentialRejected"])
    result=dict(decision, scope="PlcPolling013SoftwareOnly",components=qualified,
        negativeN3="Passed",lightweight=credential,comparison=str(folder / "compare.json"),evidence=str(folder),
        classification=decision["softwareClosure"])
    save(folder / "result.json",result); save(attempt / "final-report.json",result)
    return result


def build_after():
    import recipe_execution_010 as gate
    attempt, _, after = roots()
    folder=attempt / "after"
    if (folder / "build-manifest.json").exists():raise ValueError("AfterBuildAlreadyRecorded")
    source=gate.source_files()
    logs=folder / ("build-"+datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
    projects=["Gaode.Communication.Tests","Gaode.Contracts.Tests","Gaode.Integration.Tests","Gaode.Rules.Tests"]
    for project in projects:
        command(after,["dotnet","build",f"backend/tests/{project}/{project}.csproj","-c","Debug","-p:RestoreLockedMode=true"],logs / (project+".log"))
    products={}
    directories=[after / f"backend/tests/{p}/bin/Debug/net10.0" for p in projects]
    directories += [after / p for p in ("backend/src/Gaode.Host/bin/Debug/net10.0","backend/tools/Gaode.StorePrep/bin/Debug/net10.0","VirtualPlc/bin/Debug/net10.0")]
    for directory in directories:
        for p in directory.iterdir():
            if p.is_file():products[p.relative_to(after).as_posix()]=sha(p)
    if source!=gate.source_files():raise ValueError("SourceChangedDuringAfterBuild")
    save(folder / "build-manifest.json",dict(sourceRoot=str(after),sourceFiles=source,products=products,configuration="Debug"))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("stage", choices=("before-build", "before-measure", "after-build", "diagnostic-preflight"))
    parser.add_argument("--parent", help="Already selected final standalone request ID plus -verify-1; component evidence only")
    args = parser.parse_args()
    if args.stage == "before-build":
        build_before()
    elif args.stage == "before-measure":
        measure("before")
    elif args.stage == "diagnostic-preflight":
        diagnostic_preflight(args.parent)
    else:
        build_after()


if __name__ == "__main__":
    main()
