"""Real-process lock/race tests plus snapshot failure injection; no model/product calls."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import unittest
from unittest.mock import patch
import uuid

import runner
from locks import file_lock, WorkspaceBusy
import workspaces

AREA = Path("C:/gaode-wf-parallel") / uuid.uuid4().hex[:8]


class Checks(unittest.TestCase):
    def setUp(self):
        self.area = AREA / self._testMethodName
        self.source = self.area / "source"
        self.parent = Path("C:/gw") / uuid.uuid4().hex[:8]
        self.source.mkdir(parents=True)
        (self.source / "dev.ps1").write_text("# fixture", encoding="utf-8")
        self.processes = []

    def tearDown(self):
        for p in self.processes:
            if p.poll() is None:
                p.kill()
            p.wait(timeout=5)

    def holder(self, path, *, seconds=1, slot=False):
        ready = self.area / (uuid.uuid4().hex[:6] + ".ready")
        command = [sys.executable, "-B", __file__, "--hold", str(path), "--ready", str(ready),
                   "--seconds", str(seconds)]
        if slot:
            command.append("--slot")
        p = subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        self.processes.append(p)
        deadline = time.monotonic() + 10
        while not ready.exists():
            if p.poll() is not None or time.monotonic() > deadline:
                self.fail("lock holder did not start")
            time.sleep(0.05)
        return p

    def test_real_lock_contention_and_wait(self):
        path = self.area / "runner.lock"
        p = self.holder(path)
        with self.assertRaises(WorkspaceBusy):
            with file_lock(path):
                self.fail("must not enter")
        with file_lock(path, wait_seconds=5):
            self.assertEqual(p.wait(timeout=5), 0)
        self.assertFalse(path.with_name("runner.lock.json").exists())

    def test_crash_releases_lock_stale_metadata_not_authority(self):
        path = self.area / "runner.lock"
        p = self.holder(path, seconds=60)
        p.kill()
        p.wait(timeout=5)
        with file_lock(path, wait_seconds=5):
            self.assertEqual(runner.load(path.with_name("runner.lock.json"))["pid"], os.getpid())

    def test_different_workspaces_do_not_block(self):
        self.holder(self.area / "a/runner.lock", seconds=10)
        with file_lock(self.area / "b/runner.lock"):
            pass

    def test_two_slots_third_rejected_then_admitted(self):
        self.holder(self.parent, seconds=1.5, slot=True)
        self.holder(self.parent, seconds=1.5, slot=True)
        with self.assertRaises(WorkspaceBusy):
            with workspaces.execution_slot(self.parent, 0):
                self.fail("third slot must not enter")
        with workspaces.execution_slot(self.parent, 5):
            pass

    def test_snapshot_exclusions_and_independent_bytes(self):
        for rel in ("backend/src/App.cs", "backend/src/bin/cached.dll", "backend/tests/obj/project.json",
                    ".specify/workflows/runner.lock", "artifacts/old.log", "data/live.db"):
            p = self.source / rel
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text("original", encoding="utf-8")
        a, meta = workspaces.provision(self.source, self.parent, "alice", "900-one", True)
        b, _ = workspaces.provision(self.source, self.parent, "bob", "901-two", True)
        self.assertEqual(meta["status"], "ready")
        self.assertEqual(set(meta["files"]), {"backend/src/App.cs", "dev.ps1"})
        (a / "backend/src/App.cs").write_text("alice", encoding="utf-8")
        self.assertEqual((b / "backend/src/App.cs").read_text(), "original")
        self.assertEqual((self.source / "backend/src/App.cs").read_text(), "original")
        self.assertFalse((a / "backend/src/bin").exists())
        self.assertFalse((a / "data/live.db").exists())

    def test_duplicate_feature_and_reuse_identity_rejected(self):
        target, _ = workspaces.provision(self.source, self.parent, "alice", "900-one", True)
        with self.assertRaises(ValueError):
            workspaces.provision(self.source, self.parent, "bob", "900-another", True)
        with self.assertRaises(ValueError):
            workspaces.provision(self.source, self.parent, "alice", "901-two", True, reuse=True)
        self.assertEqual(workspaces.provision(self.source, self.parent, "alice", None, False, reuse=True)[0], target)

    def test_invalid_paths_no_mutation(self):
        for name in ("../escape", "CON", "alice.", "alice bob", ".."):
            with self.assertRaises(ValueError):
                workspaces.targets(self.source, self.parent, name)
        with self.assertRaises(ValueError):
            workspaces.targets(self.source, self.source / "nested", "alice")
        with self.assertRaises(ValueError):
            workspaces.provision(self.source, self.parent, "alice", "900-one", False)
        self.assertFalse(self.parent.exists())

    def test_source_mutation_fails_closed(self):
        original = workspaces.shutil.copy2
        def mutate(src, dest):
            original(src, dest)
            Path(src).write_text("changed during copy", encoding="utf-8")
        with patch.object(workspaces.shutil, "copy2", side_effect=mutate):
            with self.assertRaises(ValueError):
                workspaces.provision(self.source, self.parent, "alice", "900-one", True)
        self.assertEqual(runner.load(self.parent / "alice/workspace.json")["status"], "failed")
        with self.assertRaises(ValueError):
            workspaces.provision(self.source, self.parent, "alice", None, False, reuse=True)

    def test_same_name_creation_race(self):
        command = [sys.executable, "-B", __file__, "--create", str(self.source), "--parent", str(self.parent)]
        for _ in range(2):
            self.processes.append(subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL))
        self.assertEqual(sorted(p.wait(timeout=20) for p in self.processes), [0, 1])
        self.assertEqual(workspaces.managed(self.parent / "alice", self.source)["status"], "ready")

    def test_standalone_busy_does_not_run_tests(self):
        project = self.source
        (project / "scripts/workflow").mkdir(parents=True)
        for file in ("runner.py", "locks.py", "verify_entry.py"):
            workspaces.shutil.copyfile(runner.ROOT / "scripts/workflow" / file, project / "scripts/workflow" / file)
        self.holder(project / ".specify/workflows/runner.lock", seconds=10)
        p = subprocess.run([sys.executable, "-B", str(project / "scripts/workflow/verify_entry.py"), "--wait-seconds", "0"],
                           capture_output=True, text=True, encoding="utf-8", timeout=10,
                           env=dict(os.environ, PYTHONUTF8="1"))
        self.assertEqual(p.returncode, 1, p.stderr)
        result = json.loads(p.stderr)
        self.assertEqual(result["failure_kind"], "workspace_busy")
        self.assertIs(result["test_started"], False)
        self.assertFalse((project / "artifacts/workflow").exists())


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--hold", type=Path)
    parser.add_argument("--ready", type=Path)
    parser.add_argument("--seconds", type=float, default=1)
    parser.add_argument("--slot", action="store_true")
    parser.add_argument("--create", type=Path)
    parser.add_argument("--parent", type=Path)
    args = parser.parse_args()
    if args.hold:
        manager = workspaces.execution_slot(args.hold, 0) if args.slot else file_lock(args.hold)
        with manager:
            args.ready.write_text("ready", encoding="utf-8")
            time.sleep(args.seconds)
    elif args.create:
        workspaces.provision(args.create, args.parent, "alice", "900-one", True)
    else:
        unittest.main(argv=[sys.argv[0]], verbosity=2)
