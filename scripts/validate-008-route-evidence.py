"""Check one 008 Test run's persisted capture, worker and flip evidence."""

import argparse
import json
import sqlite3
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("evidence_root", type=Path)
parser.add_argument("--sequence", required=True, help="Comma-separated face camera pairs")
parser.add_argument("--page", action="store_true")
parser.add_argument("--slots", type=int, default=1)
args = parser.parse_args()
root = args.evidence_root.resolve()
expected = [camera for pair in args.sequence.split(",") for camera in pair for _ in range(args.slots)]
expected_flips = (len(args.sequence.split(",")) - 1) * args.slots
page = json.loads((root / "recipe-webview2-page-evidence.json").read_text(encoding="utf-8-sig")) if args.page else None
run = json.loads((root / "page-api-device-facts.json").read_text(encoding="utf-8-sig"))["run"] if args.page else json.loads((root / "run-latest.json").read_text(encoding="utf-8-sig"))
run_id = run["runId"]
con = sqlite3.connect(f"file:{(root / 'station01.test.db').as_posix()}?mode=ro", uri=True)
counts = dict(con.execute("select Kind,count(*) from Writes where lower(RunId)=? group by Kind", (run_id.lower(),)))
events = list(con.execute("select Stage,EventType,ErrorCode from StageEvents where lower(RunId)=?", (run_id.lower(),)))
captures = []
flips = []
for line in (root / "logs" / "host.out.log").open(encoding="utf-8-sig", errors="replace"):
    if "RuntimeFlow {" not in line:
        continue
    try:
        item = json.loads(line.split("RuntimeFlow ", 1)[1])
    except json.JSONDecodeError:
        continue
    if (item.get("runId") or "").lower() != run_id.lower():
        continue
    facts = item.get("facts") or {}
    if item.get("step") == "DetectionCapture" and item.get("outcome") == "Requesting":
        captures.append({"camera": facts.get("Camera"), "step": facts.get("Sequence")})
    if item.get("step") == "DetectionMove" and item.get("outcome") == "Dispatching" and facts.get("role") == "Flip":
        flips.append(facts.get("target"))
checks = {
    "same_run_page_final": not args.page or (page["outcome"] == "FinalPageDisplayed" and page["receipt"]["body"]["runId"].lower() == run_id.lower()),
    "run_final": run["finalOutcome"] == 1 and run["state"] == 26 and not run.get("errorCode"),
    "camera_sequence": [item["camera"] for item in captures] == expected,
    "media_count": counts.get("Media", 0) == 2 + len(expected),
    "worker_intents": counts.get("AlgorithmIntent", 0) == 2 + len(expected) + len(args.sequence.split(",")) * args.slots,
    "flip_actions": len(flips) == expected_flips,
    "stages": all(any(stage == name and kind == required and not error for stage, kind, error in events)
                  for name, required in (("Detection", "Completed"),
                                         ("UnloadPreparation", "Completed"),
                                         ("Sorting", "Completed"),
                                         ("UnlockObservation", "ObservedUnlocked"),
                                         ("ManualTrayRemovalConfirmation", "FinalUnloadCompleted"))),
}
result = {"source": "Test/VirtualPlc", "pageVerified": args.page, "runId": run_id,
          "sequence": args.sequence, "actualCameras": captures, "flipActions": len(flips),
          "expectedFlipActions": expected_flips, "writeCounts": counts,
          "checks": checks, "passed": all(checks.values())}
path = root / "route-validation.json"
with path.open("x", encoding="utf-8") as output:
    json.dump(result, output, ensure_ascii=False, indent=2)
print(path)
if not result["passed"]:
    raise SystemExit(1)
