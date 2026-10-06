"""Assert that the controlled VirtualPlc Flip_OK hold cannot enter face two."""

import argparse
import json
import sqlite3
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("evidence_root", type=Path)
root = parser.parse_args().evidence_root.resolve()
run = json.loads((root / "run-latest.json").read_text(encoding="utf-8-sig"))
con = sqlite3.connect(f"file:{(root / 'station01.test.db').as_posix()}?mode=ro", uri=True)
run_id = run["runId"]
media = con.execute("select count(*) from Writes where lower(RunId)=? and Kind='Media'", (run_id.lower(),)).fetchone()[0]
final_events = con.execute("select count(*) from StageEvents where lower(RunId)=? and EventType='FinalUnloadCompleted'", (run_id.lower(),)).fetchone()[0]
deadline = con.execute("select count(*) from StageEvents where lower(RunId)=? and ErrorCode='StageDeadlineExceeded'", (run_id.lower(),)).fetchone()[0]
captures = []
cancelled = False
for line in (root / "logs" / "host.out.log").open(encoding="utf-8-sig", errors="replace"):
    if "RuntimeFlow {" not in line:
        continue
    try:
        event = json.loads(line.split("RuntimeFlow ", 1)[1])
    except json.JSONDecodeError:
        continue
    if (event.get("runId") or "").lower() != run_id.lower():
        continue
    if event.get("step") == "DetectionCapture" and event.get("outcome") == "Requesting":
        captures.append((event.get("facts") or {}).get("Camera"))
    if event.get("step") == "DetectionPort" and event.get("outcome") == "Cancelled":
        cancelled = True
checks = {
    "fault_injected": json.loads((root / "fault.json").read_text(encoding="utf-8-sig"))["success"],
    "only_first_face_captured": captures == ["A", "B"] and media == 4,
    "deadline_and_cancel_logged": deadline > 0 and cancelled,
    "no_final": run["finalOutcome"] == 0 and final_events == 0,
}
result = {"source": "BackendAPI/TestOnly", "pageVerified": False, "runId": run_id,
          "captures": captures, "mediaCount": media,
          "checks": checks, "passed": all(checks.values())}
path = root / "fault-validation.json"
with path.open("x", encoding="utf-8") as output:
    json.dump(result, output, ensure_ascii=False, indent=2)
print(path)
if not result["passed"]:
    raise SystemExit(1)
