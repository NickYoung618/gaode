"""Workflow-only checks: real engine, fake shell/prompt; never runs product commands."""
import argparse
import io
from pathlib import Path
import unittest
from unittest.mock import patch
import uuid
import runner
from specify_cli.workflows import STEP_REGISTRY
from specify_cli.workflows.base import StepResult, StepStatus, RunStatus
from specify_cli.workflows.engine import WorkflowDefinition, WorkflowEngine


CHECK_ROOT = runner.ROOT / "artifacts" / "workflow-selfcheck" / uuid.uuid4().hex


class Checks(unittest.TestCase):
    def area(self):
        path = CHECK_ROOT / self._testMethodName
        path.mkdir(parents=True, exist_ok=True)
        return path

    def flow(self, mode, outcomes, blocked=False):
        calls, rounds, fixes = [], 0, 0
        def execute(config, context):
            nonlocal rounds, fixes
            key = config["id"].split(".")[-1]
            # Engine may namespace IDs; commands/prompts remain stable.
            text = config.get("run", "")
            calls.append(text or config.get("prompt", ""))
            if "-Action verify" in text:
                rounds += 1
            if "-Action assess" in text:
                if blocked:
                    return StepResult(status=StepStatus.FAILED, error="needs user decision")
                return StepResult(output={"data": {"needs_fix": not outcomes[min(rounds-1, len(outcomes)-1)]}})
            if "-Action begin -Phase fix" in text:
                if fixes >= 2:
                    return StepResult(status=StepStatus.FAILED, error="fix limit reached")
                fixes += 1
            return StepResult(output={"data": {"ready": True}})
        definition = WorkflowDefinition.from_yaml(runner.YAML)
        with patch.object(STEP_REGISTRY["shell"], "execute", side_effect=execute), \
             patch.object(STEP_REGISTRY["prompt"], "execute", side_effect=execute), \
             patch("subprocess.run", side_effect=AssertionError("No external process allowed")), \
             patch("subprocess.call", side_effect=AssertionError("No external process allowed")):
            state = WorkflowEngine(self.area()).execute(definition, {"request_id": "a"*32, "mode": mode})
        return state, calls, rounds, fixes

    def test_continue_skips_design(self):
        state, calls, rounds, fixes = self.flow("continue", [True])
        self.assertEqual(state.status, RunStatus.COMPLETED)
        self.assertFalse(any("-Phase specify" in c or "-Phase plan" in c or "-Phase tasks" in c for c in calls))
        self.assertEqual((rounds, fixes), (1, 0))
        self.assertTrue(any("-Action finish" in c for c in calls))

    def test_new_includes_design(self):
        state, calls, _, _ = self.flow("new", [True])
        self.assertEqual(state.status, RunStatus.COMPLETED)
        for phase in ("specify", "plan", "tasks"):
            self.assertTrue(any("-Action accept -Phase " + phase in c for c in calls))

    def test_fix_then_pass(self):
        state, _, rounds, fixes = self.flow("continue", [False, True])
        self.assertEqual(state.status, RunStatus.COMPLETED)
        self.assertEqual((rounds, fixes), (2, 1))

    def test_fix_cap_no_done(self):
        state, calls, rounds, fixes = self.flow("continue", [False])
        self.assertEqual(state.status, RunStatus.FAILED)
        self.assertEqual((rounds, fixes), (3, 2))
        self.assertFalse(any("-Action finish" in c for c in calls))

    def test_blocked_no_fix_or_done(self):
        state, calls, _, fixes = self.flow("continue", [False], blocked=True)
        self.assertEqual(state.status, RunStatus.FAILED)
        self.assertEqual(fixes, 0)
        self.assertFalse(any("-Action finish" in c for c in calls))

    def test_m1_actual_closure(self):
        rows = runner.task_rows(runner.feature_path(runner.STATION) / "tasks.md")
        # 001 retains T001-T067 and now declares later increments (tasks.md:14).
        # Protect the original tasks and exact M1 dependency closure; a newer
        # task outside that closure does not change the meaning of M1.
        self.assertTrue({f'T{i:03d}' for i in range(1, 68)}.issubset(rows))
        expected = [f'T{i:03d}' for i in (*range(1, 26), *range(30, 39))]
        self.assertEqual(runner.select_tasks(runner.STATION, "M1", rows), expected)

    def test_reject_cycle_missing_and_scope(self):
        with self.assertRaises(ValueError):
            runner.select_tasks("002-test", "All", {"T001": {"deps": ["T002"]}, "T002": {"deps": ["T001"]}})
        with self.assertRaises(ValueError):
            runner.select_tasks("002-test", "All", {"T001": {"deps": ["T099"]}})
        with self.assertRaises(ValueError):
            runner.feature_path("../outside")

    def test_trx_requires_all_executed_passed(self):
        p = self.area() / "sample.trx"
        for total, executed, passed, expected in [(0,0,0,False),(2,1,1,False),(2,2,1,False),(2,2,2,True)]:
            p.write_text(f'<TestRun><ResultSummary><Counters total="{total}" executed="{executed}" passed="{passed}"/></ResultSummary></TestRun>')
            self.assertEqual(runner.read_trx(p), expected)

    def test_required_rows_are_fixed_before_discovery(self):
        manifest=runner.required_manifest()
        rows=manifest['cases']
        self.assertEqual(sum(r['caseId'].startswith('PD-') for r in rows),12)
        self.assertEqual(sum(r['caseId'].startswith('SU') for r in rows),6)
        self.assertIn('Gaode.Communication.Tests',runner.SUITES)
        self.assertIn('BA03-healthy-wait/capacity',{r['caseId'] for r in rows})
        self.assertIn('BA03-healthy-wait/none',{r['caseId'] for r in rows})

    def test_stale_report_rejected(self):
        folder = self.area()
        current = dict(request_id="a", phase="review", nonce="new", report="report.json")
        report = dict(request_id="a", phase="review", nonce="old", status="pass",
                      summary="过期报告", evidence=["dev.ps1"], issues=[])
        runner.save(folder / "report.json", report)
        with self.assertRaises(ValueError):
            runner.check_report({}, folder, {"current": current})
        report["nonce"] = "new"
        runner.save(folder / "report.json", report)
        self.assertEqual(runner.check_report({}, folder, {"current": current})["status"], "pass")

    def test_start_without_idle_never_dispatches(self):
        with patch("subprocess.call", side_effect=AssertionError("Must not launch")):
            with self.assertRaises(ValueError):
                runner.dispatch(argparse.Namespace(workspace_idle=False))

    def test_real_fix_counter_is_persistent(self):
        folder = self.area()
        control = {"fix_count": 0}
        with patch.object(runner, "context", side_effect=lambda: ({"request_id": "test"}, folder, control)), \
             patch.object(runner, "verify_protected"):
            runner.step("begin", "fix")
            self.assertEqual(runner.load(folder / "control.json")["fix_count"], 1)
            runner.step("begin", "fix")
            self.assertEqual(runner.load(folder / "control.json")["fix_count"], 2)
            with self.assertRaises(ValueError):
                runner.step("begin", "fix")

    def test_failed_stage_resume_reenters_prompt(self):
        calls = []
        reject = [True]
        def execute(config, context):
            text = config.get("run", "")
            calls.append(text or config.get("prompt", ""))
            if "-Action accept -Phase analyze" in text and reject[0]:
                return StepResult(status=StepStatus.FAILED, error="blocked")
            return StepResult(output={"data": {"needs_fix": False}})
        engine = WorkflowEngine(self.area())
        definition = WorkflowDefinition.from_yaml(runner.YAML)
        with patch.object(STEP_REGISTRY["shell"], "execute", side_effect=execute), \
             patch.object(STEP_REGISTRY["prompt"], "execute", side_effect=execute), \
             patch("subprocess.run", side_effect=AssertionError("No external process allowed")):
            state = engine.execute(definition, {"request_id": "a"*32, "mode": "continue"})
            self.assertEqual(state.status, RunStatus.FAILED)
            self.assertEqual(runner.load(state.runs_dir / "inputs.json")["inputs"]["request_id"], "a"*32)
            reject[0] = False
            resumed = engine.resume(state.run_id)
        self.assertEqual(resumed.status, RunStatus.COMPLETED)
        self.assertEqual(sum("prompt-stage.ps1 -Phase analyze" in c for c in calls), 2)


if __name__ == "__main__":
    CHECK_ROOT.mkdir(parents=True, exist_ok=True)
    stream = io.StringIO()
    result = unittest.TextTestRunner(stream=stream, verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Checks))
    (CHECK_ROOT / "results.txt").write_text(stream.getvalue(), encoding="utf-8")
    print(stream.getvalue())
    print("Evidence:", CHECK_ROOT.relative_to(runner.ROOT))
    raise SystemExit(0 if result.wasSuccessful() else 1)
