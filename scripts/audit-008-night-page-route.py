"""Audit scene-specific facts only after the owned WPF job has exited and cleaned up."""
import json
import sqlite3
import sys
from pathlib import Path
from semantic_009_evidence import sorting_commits

def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def prop(value, name, default=None):
    return next((v for k, v in value.items() if k.lower() == name.lower()), default)

root = Path(sys.argv[1]).resolve()
if (root / "scene-acceptance-audit.json").exists():
    raise SystemExit("Existing scene audit retained; use a new actual job for revised acceptance")
job_id = root.parent.name.split("-", 2)[:2]
batch = root.parents[2]
job = load(batch / "queue" / ("-".join(job_id) + ".result.json"))
if not job["cleanupVerified"]:
    raise SystemExit("Refusing SQLite readback before verified job cleanup")
facts = load(root / "page-api-device-facts.json")
page = load(root / "recipe-webview2-page-evidence.json")
fixture = load(Path(load(root / "page-case.json")["fixture"]))
recipe = next(r for r in load(Path(fixture["recipeCatalogPath"]))["recipes"]
              if r["recipeId"] == fixture["recipeRef"]["recipeId"])
run = facts["run"]
run_id = run["runId"]
con = sqlite3.connect(f"file:{(root / 'station01.test.db').as_posix()}?mode=ro", uri=True)
committed_rows = [(event_id.lower(), json.loads(p)) for event_id, p in con.execute(
    "select EventId,PayloadJson from StageEvents where lower(RunId)=? order by Sequence", (run_id.lower(),))]
events = [payload for _, payload in committed_rows]
con.close()
results = run["results"]
transfers = sorting_commits(committed_rows, run_id)
pick_count = sum(prop(p,"kind") == "SortingAssignmentInTransit" for p in events)
place_count = len(transfers)
positions = [p for p in recipe["positions"] if p["slotId"] in fixture["occupiedSlots"]]
faces = [r for r in results if r["kind"] == "Face"]
expected_faces = len({(t["material"], t["localFace"], p["slotId"])
    for stage in recipe["execution"]["stages"] for t in stage["targets"] for p in positions
    if any(m["material"] == t["material"] for m in p["members"])})
checks = {
    "formal_page_and_same_run": job["exitCode"] == 0 and page["outcome"] == "FinalPageDisplayed"
        and page["receipt"]["body"]["runId"].lower() == run_id.lower(),
    "all_required_face_results_retained": len(faces) == expected_faces,
    "refresh_and_reopen_verdict_retained": page["resultBeforeRefresh"]["verdict"]
        == page["resultAfterRefresh"]["verdict"] == page["resultAfterReopen"]["verdict"],
}
physical = [r for r in results if r["kind"] in ("Single", "Member", "Assembly")]
movements = run.get("movements", [])
ordinary_ok = {entity for payload in events for entity in prop(payload, "ordinaryOk", [])}
checks["committed_physical_disposition_not_quality_or_final_inference"] = bool(physical) and all(
    r.get("dispositionState") == "Completed" and any(m["entityId"] == r["id"] and m["state"] == "Completed"
        and m["committedEventId"].lower() in {event_id for event_id, _ in committed_rows} for m in movements)
    or r.get("dispositionState") == "NoMoveRequired" and r["id"] in ordinary_ok for r in physical)
checks["group_part_face_do_not_copy_physical_disposition"] = all(r.get("dispositionState") is None
    for r in results if r["kind"] in ("Group", "Part", "Face"))
case = fixture["caseId"]
units = [r for r in results if r["kind"] in ("Member", "Assembly", "Part")]
if recipe["unitKind"] == "looseGroup":
    members = [r for r in results if r["kind"] == "Member"]
    groups = [r for r in results if r["kind"] == "Group"]
    checks["two_complete_groups_and_members"] = len(groups) >= 2 and len(members) == sum(len(p["members"]) for p in positions)
    problem = [m for m in members if m["disposition"] != "OK"]
    checks["only_problem_members_physically_sorted"] = pick_count == place_count == len(problem)
    if case in ("GROUP-F-MIXED", "GROUP-F-PENDING"):
        wanted = "NG" if case == "GROUP-F-MIXED" else "Pending"
        checks["expected_single_problem_member_and_others_ok"] = len(problem) == 1 and problem[0]["disposition"] == wanted
        checks["problem_is_original_p01_m01"] = len(problem) == 1 and problem[0]["id"].endswith(":G:P01:M01")
        checks["pending_face_detail_not_lost"] = any(r["disposition"] == "Pending" for r in faces)
    if case == "GROUP-A-E":
        checks["eight_source_members_all_ok"] = len(members) == 8 and all(m["disposition"] == "OK" for m in members)
elif recipe["unitKind"] == "assembledEntity":
    assemblies = [r for r in results if r["kind"] == "Assembly"]
    parts = [r for r in results if r["kind"] == "Part"]
    checks["all_parts_and_one_physical_assembly"] = len(parts) == sum(len(p["members"]) for p in positions) and len(assemblies) == len(positions)
    if not case.startswith("ROT-"):
        problem = [a for a in assemblies if a["disposition"] != "OK"]
        checks["whole_assembly_sorted_once_without_part_grabs"] = pick_count == place_count == len(problem)
    if case in ("ASSEMBLY-A-E-NG", "ASSEMBLY-A-E-PENDING"):
        checks["whole_disposition_and_pending_detail"] = len(assemblies) == 1 and assemblies[0]["disposition"] == ("NG" if case.rsplit("-", 1)[-1] == "NG" else "Pending") and any(r["disposition"] == "Pending" for r in faces)

bindings = [e for e in events if prop(e, "kind") == "ECodeBinding"]
if recipe.get("eCode", {}).get("enabled"):
    checks["e_bound_to_each_actual_primary_object"] = len(bindings) == len(positions) and all(prop(e, "objectId", "").endswith(":M01") for e in bindings)
    if case.endswith("NOCODE") or case.endswith("ERROR"):
        issue = "ECodeNoResult" if case.endswith("NOCODE") else "ControlledEDecodeFailure"
        checks["e_issue_persisted_and_whole_ok_continued"] = all(prop(e, "issue") == issue for e in bindings) and all(a["disposition"] == "OK" for a in results if a["kind"] == "Assembly")
    else:
        checks["actual_e_code_contains_object_identity"] = all(prop(e, "externalCode") and prop(e, "objectId") in prop(e, "externalCode") for e in bindings)

if case.startswith("ROT-"):
    special = [prop(p,"result",{}) for p in events if prop(p,"kind") == "SpecialExitCompleted"]
    exits = [a for a in special if prop(prop(a, "request"), "kind") == "Exit"]
    wanted = case.rsplit("-", 1)[1].replace("PENDING", "Pending")
    checks["special_exit_matches_actual_disposition_and_releases"] = len(exits) == len(positions) and all(prop(prop(a, "request"), "poseId") == wanted and prop(prop(a,"evidence",{}),"meaning") == "MaterialTransferred" and prop(prop(a,"evidence",{}),"isCorrelated") is True and prop(a, "occupiedEntityId") is None for a in exits)
    checks["no_outer_sorting_replay"] = pick_count == place_count == 0

if case.endswith("MANUAL"):
    checks["actual_page_manual_confirmation"] = any(i["action"] == "confirm-manual-flip-on-page" for i in page["interactions"])

audit = {"caseId": case, "runId": run_id, "checks": checks, "passed": all(checks.values()),
         "pickCount": pick_count, "placeCount": place_count, "faceCount": len(faces),
         "communicationExpectation": {"sorts": place_count, "manual": case.endswith("MANUAL"),
             "special": case.startswith("ROT-"), "exits": len(positions),
             "pose": case.rsplit("-",1)[1].replace("PENDING","Pending")},
         "scope": "Test scene-specific readback after verified cleanup; capacity reservation requires separate T057 evidence"}
with (root / "scene-acceptance-audit.json").open("x", encoding="utf-8") as output:
    json.dump(audit, output, ensure_ascii=False, indent=2)
print(json.dumps(audit, ensure_ascii=True))
raise SystemExit(0 if audit["passed"] else 1)
