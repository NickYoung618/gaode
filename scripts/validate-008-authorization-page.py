"""Read denied-request evidence only after the owned Test processes have exited."""
import json
import sqlite3
import sys
from pathlib import Path

root = Path(sys.argv[1])
mode = sys.argv[2]
expected = {"Auth401": 401, "Auth403": 403}[mode]
def read(name):
    return json.loads((root / name).read_text(encoding="utf-8-sig"))
page = read(f"{mode.lower()}-webview2-page-evidence.json")
facts = read("authorization-api-device-facts.json")
posts = [x for x in page["network"] if x.get("method") == "POST" and x.get("path") == "/api/v1/station01/runs"]
failures = [x for x in page["pageDiagnostics"] if x.get("event") == "StartFailed"]
final = next(x for x in reversed(page["states"]) if x["name"] == "final")
with sqlite3.connect(f"file:{(root / 'station01.test.db').as_posix()}?mode=ro", uri=True) as db:
    counts = {table: db.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0]
              for table in ("Runs", "Commands", "Writes", "Operations", "AlgorithmCalls", "StageEvents")}
checks = {
    "actual_owned_wpf_page": page["source"] == "Test/actual-WPF-WebView2-CDP-mouse",
    "one_real_denied_start": len(posts) == 1 and posts[0].get("status") == expected and page["receipt"]["status"] == expected,
    "no_response_substitution": page.get("authorizationOverride", {}).get("requestCount") == 1 and page["authorizationOverride"]["responseSubstitution"] is False,
    "actual_frontend_start_failed": len(failures) == 1 and failures[0].get("httpStatus") == expected,
    "permission_state_retained": final.get("verdict") == "权限受限" and "后端拒绝访问" in (final.get("fault") or "") and str(expected) in final["fault"],
    "no_run_receipt_or_current_run": not (page["receipt"].get("body") or {}).get("runId") and facts["status"].get("currentRun") is None,
    "no_persisted_run_command_or_execution": all(n == 0 for n in counts.values()),
    "denial_not_final": page.get("outcome") == f"HTTP_{expected}",
}
result = {"source": "Actual WPF/real Host authorization/SQLite read-only after owned process cleanup/VirtualPlc",
          "mode": mode, "checks": checks, "databaseCounts": counts,
          "passed": all(checks.values()), "productionVerified": False}
with (root / "authorization-page-validation.json").open("x", encoding="utf-8") as output:
    json.dump(result, output, ensure_ascii=False, indent=2)
print(json.dumps(result, ensure_ascii=False))
sys.exit(0 if result["passed"] else 1)
