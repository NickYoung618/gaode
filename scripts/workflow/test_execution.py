"""Workflow-only report and process fault tests. No product imports or commands."""
import io
import ctypes
import json
from pathlib import Path
import sys
import time
import unittest
from unittest.mock import patch
import uuid
import execution
import prompt_stage
import runner
import resilience

AREA = runner.ROOT / "artifacts" / "workflow-selfcheck" / uuid.uuid4().hex


class Checks(unittest.TestCase):
    def setUp(self):
        self.folder = AREA / self._testMethodName
        self.folder.mkdir(parents=True)
        self.expected = dict(request_id="diagnostic", phase="analyze", nonce=uuid.uuid4().hex)
        self.evidence = self.folder / "evidence.txt"
        self.evidence.write_text("actual workflow test fixture", encoding="utf-8")
        self.report = dict(self.expected, status="pass", summary="自检报告", evidence=[self.evidence.relative_to(runner.ROOT).as_posix()], issues=[])
        self.target = self.folder / "report.json"

    def publish(self, extra_check=None):
        return prompt_stage.publish(json.dumps(self.report), self.target, self.expected, runner.ROOT, extra_check)

    def test_valid_report_atomic_publication(self):
        self.publish()
        self.assertEqual(runner.load(self.target), self.report)
        self.assertFalse(self.target.with_suffix(".pending").exists())

    def test_wrong_nonce_not_published(self):
        self.report["nonce"] = "stale"
        with self.assertRaises(ValueError): self.publish()
        self.assertFalse(self.target.exists())

    def test_missing_evidence_not_published(self):
        self.report["evidence"] = ["artifacts/no-such-workflow-evidence-" + uuid.uuid4().hex]
        with self.assertRaises(ValueError): self.publish()
        self.assertFalse(self.target.exists())

    def test_absolute_and_escaping_evidence_rejected(self):
        for rel in (str(self.evidence), "../outside.txt"):
            with self.subTest(rel=rel):
                self.report["evidence"] = [rel]
                with self.assertRaises(ValueError): self.publish()
        self.assertFalse(self.target.exists())

    def test_probe_checks_before_publication(self):
        def reject(report): raise ValueError("wrong observed token or missing successful command evidence")
        with self.assertRaises(ValueError): self.publish(reject)
        self.assertFalse(self.target.exists())

    def test_report_not_overwritten(self):
        self.target.write_text("preserve existing evidence", encoding="utf-8")
        with self.assertRaises(FileExistsError): self.publish()
        self.assertEqual(self.target.read_text(), "preserve existing evidence")

    def test_cli_exit_zero_without_final_is_not_stage_success(self):
        current = dict(self.expected, report="report.json")
        with patch.object(runner, "context", return_value=({"request_id": self.expected["request_id"]}, self.folder, {"current": current})), \
             patch.object(runner, "verify_protected"), patch.object(prompt_stage, "execute", return_value={"exit_code": 0}), \
             patch.object(sys, "argv", ["prompt_stage.py", "--phase", "analyze"]):
            with self.assertRaisesRegex(RuntimeError, "未生成结构化输出"):
                prompt_stage.main()
        self.assertFalse(self.target.exists())

    def run_child(self, program, timeout=10):
        stdout, stderr, outcome = (self.folder / name for name in ("stdout.log", "stderr.log", "execution.json"))
        execution.execute([sys.executable, "-B", "-c", program], stdout, stderr, outcome, self.expected, timeout)
        return runner.load(outcome)

    def test_actual_deadline_kills_owned_process(self):
        start = time.monotonic()
        with self.assertRaises(RuntimeError): self.run_child("import time; time.sleep(30)", timeout=1)
        self.assertLess(time.monotonic()-start, 8)
        result = runner.load(self.folder / "execution.json")
        self.assertEqual(result["failure_kind"], "execution_deadline")
        # Windows kill-on-close can report native exit 0. The explicit deadline
        # failure and wrapper exception must remain authoritative.
        self.assertEqual(result["status"], "failed")
        self.assertNotEqual(result["exit_code"], 259)  # STILL_ACTIVE

    def test_tool_pipe_failure_fails_fast(self):
        event = {"type": "item.completed", "item": {"type": "command_execution", "status": "failed", "exit_code": 1,
                 "aggregated_output": "Failed to create unified exec process: timed out after 15000ms connecting runner pipe-in"}}
        code = "import time; print(" + repr(json.dumps(event)) + ",flush=True); time.sleep(30)"
        start = time.monotonic()
        with self.assertRaises(RuntimeError): self.run_child(code)
        self.assertLess(time.monotonic()-start, 8)
        self.assertEqual(runner.load(self.folder / "execution.json")["failure_kind"], "command_executor_failure")

    def test_exit_zero_with_executor_error_is_failure(self):
        code = "import sys; print('windows sandbox failed: connecting runner pipe-in',file=sys.stderr,flush=True)"
        with self.assertRaises(RuntimeError): self.run_child(code)
        self.assertEqual(runner.load(self.folder / "execution.json")["failure_kind"], "command_executor_failure")

    def test_historical_error_read_is_not_execution_failure(self):
        event = {"type": "item.completed", "item": {"type": "command_execution", "status": "completed", "exit_code": 0,
                 "aggregated_output": "Old log: connecting runner pipe-in"}}
        self.assertFalse(execution.failed_event(json.dumps(event)))
        result = self.run_child("print(" + repr(json.dumps(event)) + ")")
        self.assertEqual(result["exit_code"], 0)
        self.assertNotIn("failure_kind", result)

    def test_live_log_not_overwritten(self):
        (self.folder / "execution.json").write_text("preserved")
        with patch.object(execution.subprocess, "Popen", side_effect=AssertionError("Must not start another child")):
            with self.assertRaises(FileExistsError): self.run_child("print('never')")
        self.assertEqual((self.folder / "execution.json").read_text(), "preserved")

    def test_job_closes_remaining_owned_descendant(self):
        child_pid = self.folder / "child.pid"
        code = ("import subprocess,sys,time; from pathlib import Path; "
                "p=subprocess.Popen([sys.executable,'-B','-c','import time; time.sleep(30)']); "
                "Path(" + repr(str(child_pid)) + ").write_text(str(p.pid)); time.sleep(0.3)")
        self.run_child(code)
        pid = int(child_pid.read_text())
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.OpenProcess.argtypes = [ctypes.c_uint32, ctypes.c_int, ctypes.c_uint32]
        kernel.OpenProcess.restype = ctypes.c_void_p
        kernel.GetExitCodeProcess.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
        kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        handle = kernel.OpenProcess(0x1000, False, pid)
        if handle:
            try:
                for _ in range(20):
                    status=ctypes.c_uint32()
                    self.assertTrue(kernel.GetExitCodeProcess(handle,ctypes.byref(status)))
                    if status.value != 259: break
                    time.sleep(0.05)
                self.assertNotEqual(status.value,259,"diagnostic descendant still running")
            finally:
                kernel.CloseHandle(handle)

    def test_capacity_before_tools_is_retryable(self):
        event = dict(type="error", message="Selected model is at capacity. Please try a different model.")
        with self.assertRaises(RuntimeError):
            self.run_child("print(" + repr(json.dumps(event)) + ",flush=True)")
        outcome = runner.load(self.folder / "execution.json")
        self.assertEqual(outcome["failure_kind"], "codex_service_capacity")
        self.assertTrue(outcome["retryable"])

    def test_capacity_after_tool_even_read_only_is_not_retryable(self):
        events = [dict(type="item.started", item=dict(type="command_execution", command="Get-Content x")),
                  dict(type="turn.failed", error=dict(message="Selected model is at capacity"))]
        with self.assertRaises(RuntimeError):
            self.run_child("print(" + repr("\n".join(map(json.dumps, events))) + ")")
        outcome = runner.load(self.folder / "execution.json")
        self.assertTrue(outcome["tool_started"])
        self.assertFalse(outcome["retryable"])

    def test_thread_started_event_is_saved_for_safe_resume(self):
        events = [dict(type="thread.started", thread_id="thread-for-resume"),
                  dict(type="turn.failed", error=dict(message="Selected model is at capacity"))]
        with self.assertRaises(RuntimeError):
            self.run_child("print(" + repr("\n".join(map(json.dumps, events))) + ")")
        outcome = runner.load(self.folder / "execution.json")
        self.assertEqual(outcome["session_id"], "thread-for-resume")

    def test_file_change_and_unknown_tools_prevent_replay(self):
        for kind in ("file_change", "mcp_tool_call", "web_search", "collab_tool_call", "future_tool"):
            self.assertTrue(execution.event_info(json.dumps(dict(type="item.started", item=dict(type=kind))))[1])

    def test_failed_command_text_does_not_trigger_capacity_retry(self):
        event = dict(type="item.completed", item=dict(type="command_execution", status="failed", exit_code=1,
                     aggregated_output="rate limit in the application under test"))
        self.assertIsNone(execution.event_info(json.dumps(event))[0])

    def test_turn_failed_with_exit_zero_is_failure(self):
        with self.assertRaises(RuntimeError):
            self.run_child("print('{\"type\":\"turn.failed\",\"error\":{\"message\":\"authentication failed\"}}')")
        self.assertEqual(runner.load(self.folder / "execution.json")["failure_kind"], "codex_authentication")

    def test_service_error_classes_are_distinct(self):
        cases = [
            ("You've hit your usage limit. Try again at 12:00", "codex_usage_limit"),
            ("HTTP 429 too many requests", "codex_rate_limit"),
            ("HTTP 503 server is overloaded", "codex_service_capacity"),
            ("stream disconnected before completion", "codex_network_transient"),
        ]
        for message, expected in cases:
            event = {"type": "turn.failed", "error": {"message": message}}
            self.assertEqual(execution.event_info(json.dumps(event))[0], expected)

    def test_log_limit_fails_closed(self):
        stdout, stderr, outcome = (self.folder / name for name in ("large.stdout", "large.stderr", "large.execution.json"))
        with self.assertRaises(RuntimeError):
            execution.execute([sys.executable, "-B", "-c", "print('x'*2000000)"],
                              stdout, stderr, outcome, self.expected, 10, max_log_bytes=1024*1024)
        result = runner.load(outcome)
        self.assertIn(result["failure_kind"], ("execution_log_limit", "disk_space_low"))
        self.assertLessEqual(stdout.stat().st_size, 1024*1024)

    def test_capacity_event_without_trailing_newline(self):
        event = json.dumps(dict(type="error", message="Selected model is at capacity"))
        with self.assertRaises(RuntimeError):
            self.run_child("import sys; sys.stdout.write(" + repr(event) + ")")
        self.assertTrue(runner.load(self.folder / "execution.json")["retryable"])

    def capacity_attempts(self, tool_started=False, second_pass=False):
        calls = []
        def fake_execute(command, stdout, stderr, outcome, expected, timeout, **kwargs):
            calls.append(command)
            if len(calls) == 2 and second_pass:
                return {"exit_code": 0, "tool_started": True}
            runner.save(outcome, dict(failure_kind="codex_service_capacity", tool_started=tool_started,
                                     retryable=not tool_started))
            raise RuntimeError("fixture capacity")
        def build(output, schema):
            return ["fixture-codex", "-o", str(output), "--output-schema", str(schema)]
        with patch.object(prompt_stage, "execute", side_effect=fake_execute), \
             patch.object(prompt_stage.time, "sleep"):
            if second_pass and not tool_started:
                _, outcome, _ = prompt_stage._run_stage_command(build, self.folder, "stage", self.expected,
                                                               runner.ROOT, False, 180)
                self.assertIn("attempt-02", outcome.name)
            else:
                with self.assertRaises(RuntimeError):
                    prompt_stage._run_stage_command(build, self.folder, "stage", self.expected,
                                                    runner.ROOT, False, 180)
        return calls

    def test_capacity_retry_is_bounded_to_four_total_attempts(self):
        # Three waits after the initial call; this is separate from the
        # business fix limit (which remains two in the workflow).
        self.assertEqual(len(self.capacity_attempts()), 4)
        summary = runner.load(self.folder / "stage.retry-summary.json")
        self.assertEqual(len(summary["attempts"]), 4)

    def test_capacity_after_tool_resumes_session_without_replaying_prompt(self):
        calls = []
        def fake_execute(command, stdout, stderr, outcome, expected, timeout, **kwargs):
            calls.append(command)
            if len(calls) == 1:
                runner.save(outcome, dict(failure_kind="codex_service_capacity", tool_started=True,
                                          retryable=False, session_id="session-123"))
                raise RuntimeError("fixture capacity")
            return {"exit_code": 0, "tool_started": True, "session_id": "session-123"}
        def build(output, schema):
            return ["fixture-codex", "exec", "--json", str(output), str(schema)]
        def resume(output, schema, session_id):
            return ["fixture-codex", "exec", "resume", session_id, "--json", str(output), str(schema)]
        with patch.object(prompt_stage, "execute", side_effect=fake_execute), \
             patch.object(prompt_stage.time, "sleep"):
            _, outcome, _ = prompt_stage._run_stage_command(build, self.folder, "stage-resume",
                                                            self.expected, runner.ROOT, False, 180,
                                                            resume_builder=resume)
        self.assertEqual(len(calls), 2)
        self.assertIn("resume", calls[1])
        self.assertIn("session-123", calls[1])
        self.assertEqual(runner.load(self.folder / "stage-resume.retry-summary.json")["resumed"], True)

    def test_manual_resume_uses_latest_capacity_attempt(self):
        logs = self.folder / "manual-resume"
        logs.mkdir()
        stem = "analyze-" + self.expected["nonce"]
        outcome = logs / (stem + ".execution.json")
        runner.save(outcome, {"failure_kind": "codex_service_capacity", "tool_started": True,
                              "session_id": "session-later"})
        self.assertEqual(prompt_stage._capacity_recovery(logs, stem),
                         (2, "resume", "session-later"))

    def test_manual_resume_fails_closed_when_tool_state_is_uncertain(self):
        logs = self.folder / "uncertain-resume"
        logs.mkdir()
        stem = "analyze-" + self.expected["nonce"]
        outcome = logs / (stem + ".execution.json")
        runner.save(outcome, {"failure_kind": "codex_service_capacity", "tool_started": True,
                              "session_id": "session-uncertain", "incomplete_tools": ["call-1"]})
        with self.assertRaisesRegex(RuntimeError, "状态不完整"):
            prompt_stage._capacity_recovery(logs, stem)

    def test_manual_usage_resume_honors_retry_not_before(self):
        logs = self.folder / "usage-resume"
        logs.mkdir()
        stem = "analyze-" + self.expected["nonce"]
        outcome = logs / (stem + ".execution.json")
        later = (resilience.utc_now() + resilience.timedelta(hours=1)).isoformat()
        runner.save(outcome, {"failure_kind": "codex_usage_limit", "tool_started": False,
                              "retry_not_before": later})
        with self.assertRaisesRegex(RuntimeError, "尚未到恢复时间"):
            prompt_stage._capacity_recovery(logs, stem)

    def test_retryable_failure_is_published_as_paused_activity(self):
        folder = self.folder / "paused-activity"
        logs = folder / "execution-logs"
        logs.mkdir(parents=True)
        expected = dict(self.expected, phase="analyze", report="report.json")
        runner.save(folder / "control.json", {"current": expected})
        stem = "analyze-" + expected["nonce"]
        outcome = logs / (stem + ".execution.json")
        runner.save(outcome, {"failure_kind": "codex_usage_limit", "tool_started": False,
                              "retry_not_before": "2099-01-01T00:00:00+00:00"})
        observed = prompt_stage._mark_retryable_pause(folder, expected, logs, stem)
        self.assertEqual(observed["failure_kind"], "codex_usage_limit")
        activity = runner.load(folder / "control.json")["activity"]
        self.assertEqual(activity["status"], "paused_retryable")
        self.assertEqual(activity["retry_not_before"], "2099-01-01T00:00:00+00:00")

    def test_capacity_after_tool_does_not_replay(self):
        self.assertEqual(len(self.capacity_attempts(tool_started=True)), 1)

    def test_capacity_retry_can_pass_and_keeps_original_failure(self):
        self.assertEqual(len(self.capacity_attempts(second_pass=True)), 2)
        self.assertEqual(runner.load(self.folder / "stage.execution.json")["failure_kind"], "codex_service_capacity")

    def repair(self, extra_check=None):
        raw = json.dumps(self.report, ensure_ascii=False)
        raw_path = self.folder / "stage.final.json"
        raw_path.write_text(raw, encoding="utf-8")
        with patch.object(prompt_stage, "execute", side_effect=AssertionError("Report repair cannot call Codex")), \
             patch.object(execution.subprocess, "Popen", side_effect=AssertionError("No tool may run")):
            return prompt_stage._repair_report(raw, raw_path, self.target, self.expected, runner.ROOT,
                                               self.folder, "stage", extra_check)

    def test_report_repair_removes_only_narrative_without_model_or_commands(self):
        self.report["evidence"].insert(0, "Get-Content退出码为0，实际读取到token。")
        repaired = self.repair()
        self.assertEqual(repaired["evidence"], [self.evidence.relative_to(runner.ROOT).as_posix()])
        self.assertEqual(repaired["status"], self.report["status"])
        self.assertIn("Get-Content", (self.folder / "stage.final.json").read_text(encoding="utf-8"))
        self.assertEqual(runner.load(self.folder / "stage.report-repair-summary.json")["model_calls"], 0)

    def test_report_repair_cannot_fix_wrong_identity(self):
        self.report["evidence"].append("说明。")
        self.report["nonce"] = "old"
        with self.assertRaises(ValueError): self.repair()
        self.assertFalse(self.target.exists())

    def test_report_repair_cannot_drop_missing_evidence_or_fabricate_path(self):
        self.report["evidence"] += ["artifacts/missing-file.json", "说明。"]
        with self.assertRaises(ValueError): self.repair()
        self.assertFalse(self.target.exists())

    def test_report_repair_does_not_promote_blocked_or_erase_issues(self):
        self.report.update(status="blocked", issues=["需要用户输入"])
        self.report["evidence"].append("说明。")
        repaired = self.repair()
        self.assertEqual(repaired["status"], "blocked")
        self.assertEqual(repaired["issues"], ["需要用户输入"])

    def test_report_repair_still_requires_probe_checks(self):
        self.report["evidence"].append("说明。")
        def reject(report): raise ValueError("proof was never executed")
        with self.assertRaisesRegex(ValueError, "proof was never executed"): self.repair(reject)
        self.assertFalse(self.target.exists())

    def test_report_repair_never_overwrites_existing_report(self):
        self.report["evidence"].append("说明。")
        self.target.write_text("preserved", encoding="utf-8")
        with self.assertRaises(FileExistsError): self.repair()
        self.assertEqual(self.target.read_text(encoding="utf-8"), "preserved")

    def test_schema_probe_evidence_is_exact_path(self):
        schema = prompt_stage.report_schema(self.expected, True)
        self.assertEqual(schema["properties"]["evidence"]["items"]["enum"],
                         ["artifacts/workflow-doctor/diagnostic/tool-proof.json"])

    def test_report_rejects_missing_summary_and_nonstrings(self):
        for key, value in (("summary", ""), ("issues", [42]), ("evidence", [42])):
            candidate = dict(self.report, **{key: value})
            with self.assertRaises(ValueError):
                prompt_stage.validate_report(json.dumps(candidate), self.expected, runner.ROOT)


if __name__ == "__main__":
    AREA.mkdir(parents=True)
    stream=io.StringIO()
    result=unittest.TextTestRunner(stream=stream,verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Checks))
    (AREA / "results.txt").write_text(stream.getvalue(),encoding="utf-8")
    print(stream.getvalue())
    print("Evidence:",AREA.relative_to(runner.ROOT))
    sys.exit(0 if result.wasSuccessful() else 1)
