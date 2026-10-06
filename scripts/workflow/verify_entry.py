"""Standalone verification entry backed by runner.run_verify.

This file intentionally does not enter the Spec Kit engine or alter a business
Workflow control file.  It creates an isolated request/evidence folder and
returns the same two-state contract used by the automatic workflow.
"""
import json
import sys
import uuid
import argparse
import re
from locks import WorkspaceBusy

import runner


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", default="Default009")
    parser.add_argument("--wait-seconds", type=int, default=600)
    parser.add_argument("--request-id", help="Explicit identity supplied by the 009 parent before verification starts.")
    args = parser.parse_args()
    if args.profile not in ('Default009', 'RecipeExecution010', 'PlcPolling013'):
        parser.error('UnknownVerificationProfile:' + args.profile)
    if not 0 <= args.wait_seconds <= 600:
        parser.error("wait-seconds must be between 0 and 600")
    identifier = args.request_id or "standalone-" + uuid.uuid4().hex
    if not re.fullmatch(r"standalone-[0-9a-f]{32}", identifier):
        parser.error("request-id must be a new standalone-UUID identity")
    folder = runner.ROOT / "artifacts" / "workflow-standalone-verify" / identifier
    folder.mkdir(parents=True, exist_ok=False)
    request = {
        "request_id": identifier,
        "mode": "verify-only",
        "profile": args.profile,
        "feature": "standalone-verify",
        "milestone": "Verify",
        "required_manifest": "scripts/workflow/013-required-cases.json" if args.profile == "PlcPolling013" else "scripts/workflow/009-required-cases.json",
    }
    control = {"verification_count": 0}
    runner.save(folder / "request.json", request)
    runner.save(folder / "control.json", control)
    try:
        with runner.workspace_lock(wait_seconds=args.wait_seconds):
            result = runner.run_verify(request, folder, control, wait_seconds=args.wait_seconds)
        final = runner.recipe010.final_gate(result.get('passed') is True, result.get('lightweight_credential'),
            result.get('acceptance_parent'), result.get('profile'), 'verify_entry')
        result['passed'] = final['passed']
        result = dict(result, request_id=identifier, status="PASS" if result["passed"] else "FAIL",
                      scope=result.get("scope", "LocalVerifyOnly"), overall009Passed=False,
                      evidence_folder=str((runner.ROOT / "artifacts" / "workflow" / identifier)
                                          .relative_to(runner.ROOT)))
        runner.save(folder / "result.json", result)
        print(json.dumps(result, ensure_ascii=False))
        return 0 if result["passed"] else 1
    except Exception as exc:
        message = str(exc)
        busy = isinstance(exc, WorkspaceBusy)
        result = dict(request_id=identifier, passed=False, status="FAIL",
                      failure_kind="workspace_busy" if busy else type(exc).__name__,
                      test_started=False if busy else None, error=message,
                      evidence_folder=str(folder.relative_to(runner.ROOT)))
        runner.save(folder / "result.json", result)
        print(json.dumps(result, ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
