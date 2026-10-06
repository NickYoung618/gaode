"""Opt-in real two-workspace smoke: copies, Codex Doctor, restore/build/test.

Does not modify business source, run a feature implementation, merge, deploy, or
connect real equipment. Evidence and workspaces are deliberately retained.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import os
from pathlib import Path
import subprocess
import sys
import time
import uuid

import runner
import workspaces
from execution import ProcessJob


def invoke(command, log, timeout):
    started = time.monotonic()
    with log.open("xb") as stream:
        process = subprocess.Popen(command, cwd=runner.ROOT,
            env=dict(os.environ, PYTHONUTF8="1", PYTHONDONTWRITEBYTECODE="1"),
            stdout=stream, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL)
        job = None
        try:
            job = ProcessJob(process)
            while process.poll() is None:
                if time.monotonic() - started > timeout:
                    raise TimeoutError(str(log))
                try:
                    process.wait(timeout=20)
                except subprocess.TimeoutExpired:
                    print(f"[Parallel smoke] running: {log.name}", flush=True)
            return dict(exit_code=process.returncode, elapsed=round(time.monotonic() - started, 2), log=str(log))
        finally:
            if job:
                job.close()
            if process.poll() is None:
                process.kill()
                process.wait(timeout=10)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-idle", action="store_true", required=True)
    parser.add_argument("--parent", type=Path, required=True)
    parser.add_argument("--doctor", action="store_true", help="also invoke real Codex; consumes account usage")
    args = parser.parse_args()
    identifier = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S-") + uuid.uuid4().hex[:4]
    evidence = runner.ROOT / "artifacts/parallel-test" / identifier
    evidence.mkdir(parents=True)
    before = workspaces.inventory(runner.ROOT)
    rows = []
    result = dict(passed=False, scope="two sessions, one OS account; no business implementation or live hardware", evidence=str(evidence))
    def run_action(row, action):
        row[action] = invoke(["pwsh", "-NoProfile", "-File", str(runner.ROOT / "dev-parallel.ps1"),
            "-WorkspaceName", row["name"], "-WorkspaceParent", str(args.parent),
            "-Reuse", "-WorkspaceIdle", "-WaitSeconds", "600", "-" + action.capitalize()], evidence / f"{row['name']}-{action}.log", 6600)
    try:
        for i, suffix in enumerate(("a", "b")):
            # Keep the root short: Windows bridge logs include request id,
            # nonce and nested test paths.
            name = f"p{identifier[-6:]}{suffix}"
            # Dedicated parent per smoke invocation avoids reusing/reserving production Feature IDs.
            feature = f"{900 + i:03d}-parallel-{suffix}"
            args_for_copy = ["pwsh", "-NoProfile", "-File", str(runner.ROOT / "dev-parallel.ps1"),
                "-WorkspaceName", name, "-WorkspaceParent", str(args.parent), "-Feature", feature, "-SourceIdle", "-CreateOnly"]
            copy = invoke(args_for_copy, evidence / f"{suffix}-copy.log", 120)
            if copy["exit_code"]:
                raise RuntimeError(f"snapshot failed: {copy}")
            rows.append(dict(name=name, workspace=str(args.parent / name), feature=feature, copy=copy))
        with ThreadPoolExecutor(max_workers=2) as pool:
            for action in (["check", "doctor", "verify"] if args.doctor else ["check", "verify"]):
                futures = [pool.submit(run_action, row, action) for row in rows]
                for f in futures:
                    f.result()
        for row in rows:
            root = Path(row["workspace"])
            row["verify_results"] = [dict(path=str(p), result=runner.load(p))
                for p in root.glob("artifacts/workflow-standalone-verify/*/result.json")]
            row["doctor_results"] = [dict(path=str(p), result=runner.load(p))
                for p in root.glob("artifacts/workflow-doctor/*/result.json")]
        result["source_unchanged"] = workspaces.inventory(runner.ROOT) == before
        ids = [r["result"]["request_id"] for row in rows for r in row["verify_results"]]
        result["unique_verify_ids"] = len(ids) == len(set(ids)) == 2
        result["passed"] = (result["source_unchanged"] and result["unique_verify_ids"]
            and all(row["verify"]["exit_code"] == 0 and row["check"]["exit_code"] == 0 for row in rows)
            and all(r["result"]["passed"] for row in rows for r in row["verify_results"])
            and (not args.doctor or all(row["doctor"]["exit_code"] == 0 for row in rows)))
    except Exception as exc:
        result["error"] = str(exc)
    finally:
        result.update(workspaces=rows, finished=runner.stamp())
        runner.save(evidence / "result.json", result)
        print(f"passed={result['passed']}; result={evidence / 'result.json'}", flush=True)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
