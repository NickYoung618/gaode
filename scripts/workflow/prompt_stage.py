"""Run a Workflow prompt and publish its checked report outside the Codex process."""

import argparse
import json
import os
from pathlib import Path
import random
import re
import sys
import time

import runner
from execution import execute
from resilience import assert_ready, recovery_mode


PHASES = {"specify", "plan", "tasks", "analyze", "implement", "review", "fix"}
# Service-capacity retries are independent from the business fix counter.  The
# overall stage deadline remains authoritative, so a busy service cannot create
# an unbounded workflow.
MAX_CAPACITY_RETRIES = 3
CAPACITY_BACKOFF_BASE_SECONDS = 30
CAPACITY_BACKOFF_MAX_SECONDS = 300
RETRYABLE_STAGE_FAILURES = {
    "codex_service_capacity", "codex_rate_limit", "codex_network_transient", "codex_usage_limit"
}
REPORT_REPAIR_LIMIT = 64 * 1024


def validate_report(raw, expected, root, extra_check=None):
    report = json.loads(raw)
    if not isinstance(report, dict):
        raise ValueError("阶段输出必须是JSON对象")
    allowed = {"request_id", "phase", "nonce", "status", "summary", "evidence", "issues"}
    if expected.get("phase") == "probe":
        allowed.add("observed_token")
    unknown = set(report) - allowed
    if unknown:
        raise ValueError("阶段报告包含未允许字段：" + ",".join(sorted(unknown)))
    for key in ("request_id", "phase", "nonce"):
        if report.get(key) != expected[key]:
            raise ValueError("阶段报告身份不匹配：" + key)
    if report.get("status") not in ("pass", "fix", "blocked"):
        raise ValueError("阶段状态无效")
    if not isinstance(report.get("summary"), str) or not report["summary"].strip():
        raise ValueError("阶段报告缺少非空summary")
    if (not isinstance(report.get("evidence"), list) or not isinstance(report.get("issues"), list)
            or any(not isinstance(item, str) for item in report["evidence"])
            or any(not isinstance(item, str) for item in report["issues"])):
        raise ValueError("缺少证据或问题数组")
    if report["status"] == "pass" and (report["issues"] or not report["evidence"]):
        raise ValueError("pass需要真实证据且不能保留问题")
    for rel in report["evidence"]:
        if not isinstance(rel, str) or Path(rel).is_absolute() or not runner.inside(root / rel, root).is_file():
            raise ValueError("证据文件不存在：" + str(rel))
    if extra_check:
        extra_check(report)
    return report


def publish(raw, target, expected, root, extra_check=None):
    report = validate_report(raw, expected, root, extra_check)
    if target.exists():
        raise FileExistsError("报告已存在，拒绝覆盖：" + str(target))
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_suffix(".pending")
    if temporary.exists():
        raise FileExistsError("存在未处理的报告暂存文件")
    with temporary.open("x", encoding="utf-8") as stream:
        stream.write(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
        stream.flush()
        os.fsync(stream.fileno())
    if os.name == "nt":
        temporary.rename(target)  # Windows refuses an existing destination.
    else:
        os.link(temporary, target)
        temporary.unlink()
    return report


def check_probe(report, folder, expected):
    challenge = runner.load(folder / "input.json")
    proof = runner.load(folder / "tool-proof.json")
    if (report["status"] != "pass" or report.get("observed_token") != challenge["token"]
            or proof != {"token": challenge["token"], "nonce": expected["nonce"], "source_exists": True}
            or (folder / "tool-proof.json").relative_to(runner.ROOT).as_posix() not in report["evidence"]):
        raise ValueError("探针未证明读取、路径检查和保存结果；不发布报告")
    logs = folder / "execution-logs"
    log_files = sorted(p for p in logs.glob(f"probe-{expected['nonce']}*.stdout.log")
                       if "report-repair" not in p.name)
    if not log_files:
        raise ValueError("探针缺少Codex执行事件日志")
    events = []
    for log in log_files:
        events.extend(json.loads(line) for line in log.read_text(encoding="utf-8").splitlines() if line.strip())
    completed = [event["item"] for event in events if event.get("type") == "item.completed"
                 and event.get("item", {}).get("type") == "command_execution"]
    successful = [item for item in completed if item.get("exit_code") == 0 and item.get("status") == "completed"]
    if len(successful) != len(completed) or not all(any(verb in item.get("command", "") for item in successful)
                                                  for verb in ("Get-Content", "Test-Path", "Set-Content")):
        raise ValueError("探针缺少实际命令成功记录；退出码和模型自述不能替代命令证据")


def report_schema(expected, probe):
    properties = {key: {"type": "string", "enum": [expected[key]]}
                  for key in ("request_id", "phase", "nonce")}
    properties.update(status={"type": "string", "enum": ["pass", "fix", "blocked"]},
                      summary={"type": "string"},
                      evidence={"type": "array", "items": {"type": "string"}},
                      issues={"type": "array", "items": {"type": "string"}})
    if probe:
        properties["observed_token"] = {"type": "string"}
        properties["evidence"]["items"]["enum"] = [
            f"artifacts/workflow-doctor/{expected['request_id']}/tool-proof.json"]
    properties["evidence"]["description"] = (
        "Only existing file paths relative to the project root, using / separators. "
        "No descriptions or command outcomes here; put explanations in summary.")
    return dict(type="object", additionalProperties=False, properties=properties, required=list(properties))


def _progress(info):
    """Emit bounded progress without mixing it into the structured stdout contract."""
    print(json.dumps({"workflow_progress": info}, ensure_ascii=False), file=sys.stderr, flush=True)


def _execution_paths(logs, stem, attempt):
    suffix = "" if attempt == 1 else f".attempt-{attempt:02d}"
    return tuple(logs / f"{stem}{suffix}.{kind}" for kind in
                 ("final.json", "stdout.log", "stderr.log", "execution.json", "schema.json"))


def _capacity_recovery(logs, stem):
    """Return a safe continuation point for an explicitly resumed stage.

    Only a prior, fully recorded retryable service failure may be resumed.  Other
    failures (including a missing/partial outcome) remain fail-closed.
    """
    pattern = re.compile(re.escape(stem) + r"(?:\.attempt-(\d{2}))?\.execution\.json$")
    candidates = []
    for path in logs.glob(f"{stem}*.execution.json"):
        match = pattern.fullmatch(path.name)
        if match:
            candidates.append((int(match.group(1) or "1"), path))
    if not candidates:
        return None
    attempt, outcome_path = max(candidates, key=lambda pair: pair[0])
    observed = runner.load(outcome_path)
    if observed.get("failure_kind") not in RETRYABLE_STAGE_FAILURES:
        return None
    if observed.get("incomplete_tools") or observed.get("tool_state_uncertain"):
        raise RuntimeError("上次服务错误时工具状态不完整或未知；拒绝resume，先人工核对工作区")
    try:
        assert_ready(observed)
    except ValueError as exc:
        raise RuntimeError(str(exc)) from exc
    mode = recovery_mode(dict(observed, status="failed"))
    if mode is None:
        if observed.get("tool_started"):
            raise RuntimeError("上次服务错误发生在工具执行后但没有可恢复session；拒绝重放阶段")
        raise RuntimeError("上次服务错误的执行状态不完整；拒绝自动重开阶段")
    return attempt + 1, mode, observed.get("session_id") or observed.get("resume_session_id")


def _capacity_backoff(attempt):
    """Return bounded exponential backoff before the next service attempt."""
    raw = os.environ.get("GAODE_WORKFLOW_CAPACITY_BACKOFF_SECONDS", "30")
    try:
        base = max(1, int(raw))
    except ValueError:
        base = CAPACITY_BACKOFF_BASE_SECONDS
    delay = base * (2 ** max(0, attempt - 1))
    try:
        jitter = min(1.0, max(0.0, float(os.environ.get(
            "GAODE_WORKFLOW_CAPACITY_JITTER", "0.20"))))
    except ValueError:
        jitter = 0.20
    return min(CAPACITY_BACKOFF_MAX_SECONDS, delay * random.uniform(1.0 - jitter, 1.0 + jitter))


def _retry_reason(attempts):
    kinds = {item.get("failure_kind") for item in attempts if item.get("failure_kind")}
    return ",".join(sorted(kinds)) or "retryable_service_failure"


def _mark_retryable_pause(folder, expected, logs, stem):
    """Persist a resumable service pause without mutating Spec Kit's state file."""
    if not folder or not (folder / "control.json").is_file():
        return None
    candidates = sorted(logs.glob(f"{stem}*.execution.json"),
                        key=lambda path: path.stat().st_mtime, reverse=True)
    observed = None
    for path in candidates:
        try:
            candidate = runner.load(path)
        except (OSError, ValueError):
            continue
        if candidate.get("failure_kind") in RETRYABLE_STAGE_FAILURES:
            observed = candidate
            break
    if not observed:
        return None
    control = runner.load(folder / "control.json")
    current = control.get("current", {})
    if current.get("nonce") != expected.get("nonce"):
        return None
    control["activity"] = {
        "phase": expected["phase"], "status": "paused_retryable",
        "failure_kind": observed.get("failure_kind"),
        "session_id": observed.get("session_id") or observed.get("resume_session_id"),
        "tool_started": bool(observed.get("tool_started")),
        "paused_at": runner.stamp(),
        "next_action": "capacity/服务恢复后使用 dev.ps1 -Resume <run_id> -WorkspaceIdle",
    }
    for key in ("retry_hint", "retry_not_before"):
        if observed.get(key):
            control["activity"][key] = observed[key]
    control["retry"] = dict(failure_kind=observed.get("failure_kind"),
                             session_id=observed.get("session_id") or observed.get("resume_session_id"),
                             tool_started=bool(observed.get("tool_started")),
                             execution=str(path.relative_to(runner.ROOT)))
    for key in ("retry_hint", "retry_not_before", "incomplete_tools", "tool_state_uncertain"):
        if observed.get(key):
            control["retry"][key] = observed[key]
    runner.save(folder / "control.json", control)
    return observed


def _run_stage_command(command_builder, logs, stem, expected, root, probe, timeout,
                       resume_builder=None, start_attempt=1, initial_mode="new",
                       initial_session=None):
    """Run a stage with bounded capacity waits and safe session continuation.

    A capacity error before any tool was started may replay the original prompt.
    Once a tool has started, the original prompt is never replayed: when the
    Codex session id is available, ``codex exec resume`` continues that session
    from its last tool boundary.  Without a session id the run fails closed.
    """
    deadline = time.monotonic() + timeout
    attempts = []
    summary_path = logs / f"{stem}.retry-summary.json"
    if summary_path.is_file():
        prior = runner.load(summary_path)
        if isinstance(prior.get("attempts"), list):
            attempts.extend(prior["attempts"])
    mode = initial_mode
    resume_session = initial_session
    max_attempt_number = MAX_CAPACITY_RETRIES + 1
    if start_attempt > max_attempt_number:
        raise RuntimeError("该阶段已达到服务重试上限；请人工检查后再决定是否新建请求")
    for offset in range(max_attempt_number - start_attempt + 1):
        attempt = start_attempt + offset
        output, stdout, stderr, outcome, schema = _execution_paths(logs, stem, attempt)
        if any(p.exists() for p in (output, stdout, stderr, outcome, schema)):
            raise FileExistsError("本次执行产物已存在，拒绝覆盖")
        if mode == "resume":
            if not resume_builder or not resume_session:
                raise RuntimeError("Codex容量错误发生在工具执行后，但缺少可恢复的session id；已拒绝重放阶段")
            command = resume_builder(output, schema, resume_session)
        else:
            command = command_builder(output, schema)
        runner.save(schema, report_schema(expected, probe))
        remaining = max(1, deadline - time.monotonic())
        try:
            result = execute(command, stdout, stderr, outcome, expected, remaining,
                             sandbox="workspace-write", progress=_progress)
            attempts.append({"attempt": attempt, "outcome": str(outcome.relative_to(root)),
                             "failure_kind": None, "tool_started": result.get("tool_started", False),
                             "mode": mode, "session_id": result.get("session_id")})
            if len(attempts) > 1:
                runner.save(summary_path, {"attempts": attempts,
                           "reason": _retry_reason(attempts),
                           "replayed": any(item.get("mode") == "new" for item in attempts[1:]),
                           "resumed": any(item.get("mode") == "resume" for item in attempts)})
            return output, outcome, result
        except RuntimeError:
            observed = runner.load(outcome) if outcome.is_file() else {"failure_kind": "unknown"}
            resume_session = observed.get("session_id") or resume_session
            if mode == "resume" and resume_session and not observed.get("session_id"):
                observed["resume_session_id"] = resume_session
                runner.save(outcome, observed)
            attempts.append({"attempt": attempt, "outcome": str(outcome.relative_to(root)),
                             "failure_kind": observed.get("failure_kind"),
                             "tool_started": observed.get("tool_started", False),
                             "mode": mode,
                             "session_id": observed.get("session_id") or observed.get("resume_session_id")})
            failure_kind = observed.get("failure_kind")
            retryable_service = failure_kind in {
                "codex_service_capacity", "codex_rate_limit", "codex_network_transient"
            }
            has_time = time.monotonic() < deadline - 1
            if mode == "resume":
                retryable = retryable_service and bool(resume_builder and resume_session)
                next_mode = "resume"
            elif observed.get("tool_started"):
                retryable = retryable_service and bool(resume_builder and resume_session)
                next_mode = "resume"
            else:
                retryable = retryable_service and observed.get("retryable") is True
                next_mode = "new"
            retryable = retryable and attempt < max_attempt_number and has_time
            if not retryable:
                if len(attempts) > 1:
                    runner.save(summary_path, {"attempts": attempts,
                               "reason": _retry_reason(attempts),
                               "replayed": any(item.get("mode") == "new" for item in attempts[1:]),
                               "resumed": any(item.get("mode") == "resume" for item in attempts)})
                raise
            delay = min(_capacity_backoff(attempt), max(0, deadline - time.monotonic() - 1))
            if delay < 1:
                raise
            _progress({"event": "service_retry", "failure_kind": failure_kind,
                       "attempt": attempt + 1,
                       "max_attempts": MAX_CAPACITY_RETRIES + 1, "mode": next_mode,
                       "delay_seconds": delay, "session_id": resume_session})
            time.sleep(delay)
            mode = next_mode
    raise RuntimeError("阶段执行未产生结果")


def _repair_report(raw, raw_path, target, expected, root, logs, stem, extra_check):
    """One deterministic transport correction, with no model or tool invocation.

    Only remove clearly narrative Chinese sentences accidentally placed beside
    real evidence paths (the observed Doctor failure). Never invent paths, change
    identity/status/issues, promote blocked to pass, or repair invalid JSON.
    """
    if len(raw) > REPORT_REPAIR_LIMIT:
        raise ValueError("阶段报告超过报告校正输入上限")
    candidate = json.loads(raw)
    if not isinstance(candidate, dict) or not isinstance(candidate.get("evidence"), list):
        raise ValueError("报告结构错误，不属于可自动校正范围")
    kept, removed = [], []
    for rel in candidate["evidence"]:
        if (isinstance(rel, str) and rel.endswith(("。", "；", "！"))
                and not any(char in rel for char in ("/", "\\", ":"))):
            removed.append(rel)
        else:
            kept.append(rel)
    if not removed or not kept:
        raise ValueError("仅允许移出误填入evidence的说明句，且必须保留真实文件路径")
    candidate["evidence"] = kept
    corrected = json.dumps(candidate, ensure_ascii=False)
    validate_report(corrected, expected, root, extra_check)
    output = logs / f"{stem}.report-repair.final.json"
    with output.open("x", encoding="utf-8") as stream:
        stream.write(corrected + "\n")
    runner.save(logs / f"{stem}.report-repair-summary.json", {
        "raw_report": str(raw_path.relative_to(root)),
        "corrected_report": str(output.relative_to(root)),
        "method": "deterministic-narrative-filter", "removed_narrative": removed,
        "model_calls": 0, "tool_calls": 0, "status": candidate["status"]})
    return publish(corrected, target, expected, root, extra_check)


def main():
    parser = argparse.ArgumentParser()
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--phase", choices=sorted(PHASES))
    group.add_argument("--probe-folder")
    args = parser.parse_args()
    if args.probe_folder:
        root = runner.ROOT
        folder = runner.inside(Path(args.probe_folder), root / "artifacts" / "workflow-doctor")
        expected = runner.load(folder / "expected.json")
        if expected["phase"] != "probe":
            raise ValueError("诊断上下文不是probe")
        rel = folder.relative_to(root).as_posix()
        prompt = ("这是Workflow基础设施探针，不是业务阶段。只执行以下诊断，不启动子代理或Workflow，"
                  "不读写业务代码/规格/数据库，不执行产品测试或设备操作。使用命令工具按顺序完成："
                  f"1. Get-Content -LiteralPath '{rel}/input.json' -Raw，读取其中token；"
                  f"2. Test-Path -LiteralPath '{rel}/input.json'；"
                  f"3. 用PowerShell把对象保存到'{rel}/tool-proof.json'，内容仅包含读到的token、"
                  f"nonce='{expected['nonce']}'和source_exists=true。不要把预期值冒充工具执行结果。"
                  "任何工具故障立即停止，不重试或修改shell/权限。最终只输出Schema要求的JSON；"
                  f"身份为{json.dumps(expected, ensure_ascii=False)}；observed_token为实际读到的token，"
                  f"summary为简短中文结论，成功时evidence必须恰为['{rel}/tool-proof.json']；"
                  "evidence不得含说明句或命令结果，说明仅放summary。成功才pass，否则blocked并写具体issues。"
                  "外层负责保存最终report.json，不得自行写report.json。")
        target = folder / "report.json"
    else:
        request, folder, control = runner.context()
        runner.verify_protected(request, control)
        expected = control["current"]
        if expected["phase"] != args.phase or expected["request_id"] != request["request_id"]:
            raise ValueError("当前阶段身份不匹配")
        root = runner.ROOT
        target = runner.inside(folder / expected["report"], folder)
        prompt = (f"这是项目Workflow的{args.phase}阶段。资料都在当前工作区，请使用Codex命令工具按需查找，"
                  "不要由外层把项目资料拼进提示词。先用rg/Select-String定位 scripts/workflow/stages.md 中与当前阶段相关的章节，"
                  "只读取命中行附近；禁止对大型规格、源码树或日志执行Get-Content -Raw、无筛选的rg或整目录输出。"
                  f"再读取 {folder.relative_to(root).as_posix()}/request.json 与 control.json。"
                  f"严格执行{args.phase}阶段规则。如有answers.md读取用户补充。保留已有实现，"
                  "按实际证据继续，不重启其他阶段。不调用dev.ps1或另启Workflow，不启动子代理，"
                  "不连接设备或生产库，不修改Workflow控制文件。"
                  "任何命令执行器连接故障立即报告并停止，不重试、扩大权限或更换执行方式。"
                  "最终回复仅为JSON对象，包含准确的request_id、phase、nonce、summary、"
                  "status(pass/fix/blocked)、evidence文件路径数组和issues数组。"
                  "报告由外层保存；不得自行写入control.current.report。"
                  f"当前身份：{expected['request_id']} {expected['phase']} {expected['nonce']}。")
    folder.mkdir(parents=True, exist_ok=True)
    target.parent.mkdir(parents=True, exist_ok=True)
    logs = folder / "execution-logs"
    logs.mkdir(parents=True, exist_ok=True)
    prefix = args.phase if not args.probe_folder else "probe"
    stem = f"{prefix}-{expected['nonce']}"
    output, stdout, stderr, outcome, schema = _execution_paths(logs, stem, 1)
    start_attempt, initial_mode, initial_session = 1, "new", None
    if output.exists() or schema.exists() or target.exists():
        recovery = _capacity_recovery(logs, stem)
        if not recovery or target.exists():
            raise FileExistsError("本阶段已有执行产物；拒绝覆盖/自动重跑，先核对原运行")
        start_attempt, initial_mode, initial_session = recovery
    total_timeout = 180 if args.probe_folder else 6900

    def command_builder(final_output, output_schema):
        return [runner.codex_binary(), "exec", "--sandbox", "workspace-write",
                "--skip-git-repo-check", "--json", "-C", str(root), "--output-schema",
                str(output_schema), "-o", str(final_output), prompt]

    def resume_builder(final_output, output_schema, session_id):
        continuation = (
            "临时服务容量错误中断了当前阶段。请从本会话最后一个已完成的工具边界继续，"
            "先检查当前工作区和已有输出；不要重复已经成功的命令，不要重新启动Workflow，"
            "不要覆盖无关文件。完成同一阶段的原目标后，仍只输出当前Schema要求的JSON报告。"
        )
        # `exec resume` inherits the original session's working directory and
        # sandbox.  Its CLI intentionally does not accept the new-exec `-C` and
        # `--sandbox` options, so do not add them here.
        return [runner.codex_binary(), "exec", "resume", session_id,
                "--skip-git-repo-check", "--json", "--output-schema",
                str(output_schema), "-o", str(final_output), continuation]

    try:
        output, outcome, result = _run_stage_command(command_builder, logs, stem, expected, root,
                                            bool(args.probe_folder), total_timeout,
                                            resume_builder=resume_builder,
                                            start_attempt=start_attempt,
                                            initial_mode=initial_mode,
                                            initial_session=initial_session)
    except RuntimeError:
        if not args.probe_folder:
            _mark_retryable_pause(folder, expected, logs, stem)
        raise
    if not output.is_file():
        result.update(status="failed", failure_kind="report_missing",
                      error="Codex退出成功但未生成结构化输出")
        runner.save(outcome, result)
        raise RuntimeError("Codex退出成功但未生成结构化输出")
    raw = output.read_text(encoding="utf-8")
    extra_check = (lambda r: check_probe(r, folder, expected)) if args.probe_folder else None
    try:
        report = publish(raw, target, expected, root, extra_check)
    except (ValueError, json.JSONDecodeError) as report_error:
        # No Codex retry: only a narrow deterministic transport correction.
        _progress({"event": "report_repair", "reason": str(report_error)[:240]})
        try:
            report = _repair_report(raw, output, target, expected, root, logs, stem, extra_check)
        except Exception as exc:
            result.update(status="failed", failure_kind="report_validation_failure",
                          error=str(exc), original_report_error=str(report_error))
            runner.save(outcome, result)
            raise
    if not args.probe_folder:
        runner.check_report(request, folder, control)
        runner.verify_protected(request, control)
    result.update(status="report_validated", report=str(target.relative_to(root)))
    runner.save(outcome, result)
    print(json.dumps({"report": str(target.relative_to(root)), "status": report["status"],
                      "execution": str(outcome.relative_to(root))}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(str(exc), file=sys.stderr)
        sys.exit(1)
