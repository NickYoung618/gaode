"""Real engine -> real Codex commands -> checked report; never enters product stages."""
import argparse
import json
import os
from pathlib import Path
import sys
import subprocess
import uuid
import yaml
import runner
from prompt_stage import check_probe
from specify_cli.workflows.base import RunStatus
from specify_cli.workflows.engine import WorkflowDefinition, WorkflowEngine


def accept(folder):
    folder = runner.inside(Path(folder), runner.ROOT / "artifacts" / "workflow-doctor")
    expected = runner.load(folder / "expected.json")
    current = dict(expected, report="report.json")
    report = runner.check_report({}, folder, {"current": current})
    check_probe(report, folder, expected)
    receipt = dict(request_id=expected["request_id"], nonce=expected["nonce"], accepted=True,
                   report_digest=runner.digest(folder / "report.json"), time=runner.stamp())
    if (folder / "acceptance.json").exists():
        raise FileExistsError("探针已有验收记录，不覆盖")
    runner.save(folder / "acceptance.json", receipt)
    return receipt


def run():
    identifier = uuid.uuid4().hex
    folder = runner.ROOT / "artifacts" / "workflow-doctor" / identifier
    folder.mkdir(parents=True, exist_ok=False)
    expected = dict(request_id=identifier, phase="probe", nonce=uuid.uuid4().hex)
    runner.save(folder / "expected.json", expected)
    runner.save(folder / "input.json", dict(token=uuid.uuid4().hex, purpose="Workflow infrastructure diagnostic only"))
    # ProbeFolder alone does not cover the real request.json -> phase -> Worker contract.
    # Exercise the real dispatcher/bridge with an isolated, non-business receiver first.
    env = dict(os.environ, PYTHONUTF8="1", PYTHONDONTWRITEBYTECODE="1")
    result = dict(passed=False, request_id=identifier, run_id=None, status="not_started",
                  failure_kind=None, error=None, evidence=str(folder.relative_to(runner.ROOT)),
                  scope="Workflow execution and report transport only; no product validation",
                  time=runner.stamp())
    try:
        with (folder / "request-context-check.log").open("w", encoding="utf-8") as log:
            context_check = subprocess.run([sys.executable, "-B", str(runner.ROOT / "scripts/workflow/test_bridge.py")],
                                           cwd=runner.ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=180)
        runner.save(folder / "request-context-check.json", dict(exit_code=context_check.returncode,
                    scope="real phase bridge, diagnostic receiver; no business stage execution"))
        if context_check.returncode != 0:
            raise RuntimeError("真实请求路径回归失败，未启动模型探针；查看 " + str(folder))
        definition = {
            "schema_version": "1.0",
            "workflow": {"id": "gaode-workflow-doctor", "name": "隔离执行与报告链路探针", "version": "1.1.0", "integration": "codex"},
            "steps": [
                {"id": "probe", "type": "shell", "run": f'pwsh -NoProfile -File "{runner.ROOT / "scripts/workflow/prompt-stage.ps1"}" -ProbeFolder "{folder}"', "output_format": "json", "timeout": 250},
                {"id": "accept", "type": "shell", "run": f'pwsh -NoProfile -File "{runner.ROOT / "scripts/workflow/doctor-accept.ps1"}" -Folder "{folder}"', "output_format": "json", "timeout": 30},
            ],
        }
        configuration = folder / "workflow.yml"
        configuration.write_text(yaml.safe_dump(definition, allow_unicode=True), encoding="utf-8")
        os.environ["GAODE_WORKFLOW_PYTHON"] = sys.executable
        os.environ["PYTHONUTF8"] = "1"
        os.environ["PYTHONDONTWRITEBYTECODE"] = "1"
        with runner.heartbeat(folder, "Doctor"):
            state = WorkflowEngine(folder).execute(WorkflowDefinition.from_yaml(configuration), {})
        passed = state.status == RunStatus.COMPLETED and (folder / "acceptance.json").is_file()
        result.update(passed=passed, run_id=state.run_id, status=state.status.value,
                      failure_kind=None if passed else "workflow_not_completed",
                      error=getattr(state, "error", None))
        if not passed and not result["error"]:
            result["error"] = "Workflow未完成或未生成验收回执"
    except subprocess.TimeoutExpired as exc:
        result.update(failure_kind="context_check_timeout", error=str(exc))
    except Exception as exc:
        result.update(failure_kind=type(exc).__name__, error=str(exc))
    executions = sorted((folder / "execution-logs").glob("probe-*.execution.json"),
                        key=lambda p: p.stat().st_mtime)
    result["attempts"] = [dict(execution=str(p.relative_to(runner.ROOT)), **runner.load(p))
                          for p in executions]
    if not result["passed"] and executions:
        last = runner.load(executions[-1])
        result.update(failure_kind=last.get("failure_kind", result["failure_kind"]),
                      error=last.get("error") or last.get("failure_detail") or result["error"])
    result["time"] = runner.stamp()
    runner.save(folder / "result.json", result)
    print(json.dumps(result, ensure_ascii=False))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--accept")
    args = parser.parse_args()
    try:
        if args.accept:
            print(json.dumps(accept(args.accept), ensure_ascii=False))
        else:
            sys.exit(run())
    except Exception as exc:
        print(str(exc), file=sys.stderr)
        sys.exit(1)
