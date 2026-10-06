"""Real dispatch/PowerShell/session bridge/context regression in an isolated fixture.

Only the stage receiver is a diagnostic stub. No Codex/model, business Workflow,
product stage, database or device is invoked by this test suite.
"""
import argparse
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import unittest
from unittest.mock import patch
import uuid
import runner

AREA = runner.ROOT / "artifacts" / "workflow-selfcheck" / uuid.uuid4().hex
RECEIVER = '''import argparse,json,os
import runner
parser=argparse.ArgumentParser()
parser.add_argument('--phase',required=True)
args=parser.parse_args()
request,folder,control=runner.context()
assert args.phase == control['current']['phase'] == 'analyze'
assert control['current']['request_id'] == request['request_id']
assert os.environ['GAODE_WORKFLOW_REQUEST'] == str(folder/'request.json')
result=dict(diagnostic='request-context-only',request_file=os.environ['GAODE_WORKFLOW_REQUEST'],
            request_id=request['request_id'],phase=args.phase,nonce=control['current']['nonce'])
runner.save(folder/'observed-context.json',result)
print(json.dumps(result))
'''


class Checks(unittest.TestCase):
    def setUp(self):
        # Keep fixture paths below Windows MAX_PATH without changing machine policy.
        self.area = AREA / uuid.uuid5(uuid.NAMESPACE_URL, self._testMethodName).hex[:8]
        self.project = self.area / "p space"
        self.scripts = self.project / "scripts" / "workflow"
        self.scripts.mkdir(parents=True)
        runner.save(self.area / "test.json", {"test": self._testMethodName})
        for name in ("runner.py", "locks.py", "session-bridge.ps1", "prompt-stage.ps1"):
            shutil.copyfile(runner.ROOT / "scripts" / "workflow" / name, self.scripts / name)
        (self.scripts / "prompt_stage.py").write_text(RECEIVER, encoding="utf-8")
        self.requests = self.project / ".specify" / "workflows" / "requests"
        self.identifier = uuid.uuid4().hex
        self.folder = self.requests / self.identifier
        self.expected = dict(request_id=self.identifier, phase="analyze", nonce=uuid.uuid4().hex)
        runner.save(self.folder / "request.json", {"request_id": self.identifier})
        runner.save(self.folder / "control.json", {"current": self.expected})

    def invoke(self, path, tag, env=None):
        values = dict(os.environ if env is None else env)
        values.update(GAODE_WORKFLOW_REQUEST=str(path), GAODE_WORKFLOW_PYTHON=sys.executable,
                      PYTHONUTF8="1", PYTHONDONTWRITEBYTECODE="1")
        # Invoke the real bridge explicitly so this check also covers it when
        # Doctor itself was launched from a nonzero Windows session.
        command = ["pwsh", "-NoProfile", "-File", str(self.scripts / "session-bridge.ps1"), "-Phase", "analyze"]
        result = subprocess.run(command, cwd=self.project, env=values, capture_output=True,
                                text=True, encoding="utf-8", timeout=90)
        (self.area / (tag + ".stdout.log")).write_text(result.stdout, encoding="utf-8")
        (self.area / (tag + ".stderr.log")).write_text(result.stderr, encoding="utf-8")
        runner.save(self.area / (tag + ".execution.json"), dict(command=command,
                    request_file=str(path), exit_code=result.returncode, diagnostic_receiver=True))
        return result

    def test_dispatch_file_through_real_phase_bridge_and_worker(self):
        (self.project / "specs" / runner.STATION).mkdir(parents=True)
        observed = {}

        def capture_dispatch(command, cwd, env):
            # Intercept only the business engine launch. Run the actual phase bridge
            # with dispatch's untouched request-file environment and a safe receiver.
            request_file = Path(env["GAODE_WORKFLOW_REQUEST"])
            self.assertEqual(request_file.name, "request.json")
            self.assertTrue(request_file.is_file())
            request = runner.load(request_file)
            expected = dict(request_id=request["request_id"], phase="analyze", nonce=uuid.uuid4().hex)
            runner.save(request_file.parent / "control.json", {"current": expected})
            runner.save(self.area / "dispatch-intercept.json", dict(command_not_executed=command,
                        request_file=str(request_file), cwd=str(cwd), purpose="context contract only"))
            result = self.invoke(request_file, "actual-phase", env)
            self.assertEqual(result.returncode, 0, result.stderr)
            observed.update(json.loads(result.stdout))
            self.assertEqual(observed, runner.load(request_file.parent / "observed-context.json"))
            self.assertEqual(observed["request_file"], str(request_file))
            self.assertEqual(observed["nonce"], expected["nonce"])
            bridge = request_file.parent / "execution-logs" / ("bridge-" + expected["nonce"])
            manifest = runner.load(bridge / "manifest.json")
            self.assertEqual(manifest["request_file"], str(request_file))
            self.assertNotIn("request_folder", manifest)
            self.assertNotEqual(runner.load(bridge / "worker-start.json")["session_id"], 0)
            self.assertEqual(runner.load(bridge / "dispatch-result.json")["exit_code"], 0)
            return result.returncode

        args = argparse.Namespace(command="start", workspace_idle=True, mode="continue", milestone="M1",
                                  feature=runner.STATION, requirement="Isolated request-path contract test")
        with patch.object(runner, "ROOT", self.project), patch.object(runner, "WF", self.project / ".specify/workflows"), \
             patch.object(runner, "check"), patch.object(runner, "protected_files", return_value={}), \
             patch.object(runner.subprocess, "call", side_effect=capture_dispatch):
            self.assertEqual(runner.dispatch(args), 0)
        self.assertEqual(observed["diagnostic"], "request-context-only")

    def test_directory_rejected_before_launch(self):
        result = self.invoke(self.folder, "directory")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("必须指向现有request.json文件", result.stderr)
        self.assertFalse((self.folder / "execution-logs").exists())

    def test_missing_file_rejected_before_launch(self):
        result = self.invoke(self.requests / uuid.uuid4().hex / "request.json", "missing")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("必须指向现有request.json文件", result.stderr)

    def test_wrong_request_id_rejected_before_launch(self):
        runner.save(self.folder / "request.json", {"request_id": "different"})
        result = self.invoke(self.folder / "request.json", "identity")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("桥接请求身份不匹配", result.stderr)
        self.assertFalse((self.folder / "execution-logs").exists())

    def test_wrong_phase_rejected_before_launch(self):
        runner.save(self.folder / "control.json", {"current": dict(self.expected, phase="implement")})
        result = self.invoke(self.folder / "request.json", "phase")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("桥接阶段不匹配", result.stderr)
        self.assertFalse((self.folder / "execution-logs").exists())

    def test_outside_requests_rejected_before_launch(self):
        outside = self.project / "request.json"
        runner.save(outside, {"request_id": self.identifier})
        result = self.invoke(outside, "outside")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("路径不在允许目录", result.stderr)


if __name__ == "__main__":
    AREA.mkdir(parents=True)
    stream = io.StringIO()
    result = unittest.TextTestRunner(stream=stream, verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Checks))
    (AREA / "results.txt").write_text(stream.getvalue(), encoding="utf-8")
    print(stream.getvalue())
    print("Evidence:", AREA.relative_to(runner.ROOT))
    sys.exit(0 if result.wasSuccessful() else 1)
