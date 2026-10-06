"""Read back a current fixture's same-run WPF/SQLite/media evidence.

Writes a new validation result in the new run directory; never updates historical runs.
"""
import argparse
import hashlib
import json
import os
import sqlite3
from pathlib import Path
from semantic_009_evidence import sorting_commits

def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def prop(value, name, default=None):
    return next((v for k, v in value.items() if k.lower() == name.lower()), default)

def media_path(path):
    # Preserve actual file access when the controlled evidence root plus media
    # identities exceed Win32 MAX_PATH. This is not a missing-file fallback.
    return Path('//?/' + str(path.absolute())) if os.name == 'nt' else path

parser = argparse.ArgumentParser()
parser.add_argument("evidence_root", type=Path)
parser.add_argument("fixture", type=Path)
args = parser.parse_args()
root = args.evidence_root.resolve()
store = Path(load(root/'storage-location.json')['testRoot']) if (root/'storage-location.json').is_file() else root
fixture = load(args.fixture)
catalog_path = Path(fixture["recipeCatalogPath"])
catalog_digest = hashlib.sha256(catalog_path.read_bytes()).hexdigest().upper()
recipe = next(r for r in load(catalog_path)["recipes"]
              if r["recipeId"] == fixture["recipeRef"]["recipeId"])
page = load(root / "recipe-webview2-page-evidence.json")
facts = load(root / "page-api-device-facts.json")
run_id = facts["run"]["runId"].lower()
con = sqlite3.connect((store/'station01.test.db').resolve().as_uri()+'?mode=ro', uri=True)
rows = list(con.execute("select Kind,PayloadJson from Writes where lower(RunId)=? order by Revision", (run_id,)))
writes = [(kind, load_value) for kind, value in rows for load_value in [json.loads(value)]]
events = [(stage, kind, error, json.loads(value)) for stage, kind, error, value in con.execute(
    "select Stage,EventType,ErrorCode,PayloadJson from StageEvents where lower(RunId)=? order by Sequence", (run_id,))]
captures = [p for _, _, _, p in events if prop(p, "relativeKey") and prop(p, "stepSequence") and prop(p, "camera") in ("A", "B", "C", "D")]
actual = [(prop(p, "camera"), prop(p, "localFace"), prop(p, "objectId").split(":", 1)[1]) for p in captures]
positions = [p for p in recipe["positions"] if p["slotId"] in fixture["occupiedSlots"]]
def expected_stage(stage, batch):
    expected = []
    pairs = list(dict.fromkeys(t["cameraPair"] for t in stage["targets"]))
    for pair in pairs:
        for camera in pair:
            for target in [t for t in stage["targets"] if t["cameraPair"] == pair]:
                for position in batch:
                    for member in position["members"]:
                        if member["material"] != target["material"]:
                            continue
                        unit = position["unitIdPattern"].replace("{TrayRunId}", "RUN")
                        identity = member["memberIdPattern"].replace("{UnitId}", unit).split(":", 1)[1]
                        expected.append((camera, target["localFace"], identity))
    return expected
stages = recipe["execution"]["stages"]
special = recipe["execution"]["route"].startswith("specialType1")
expected = [item for batch in ([p] for p in positions) for s in stages for item in expected_stage(s, batch)] if special else [item for s in stages for item in expected_stage(s, positions)]
face_count = len(set((face, identity) for _, face, identity in expected))
e_count = len(positions) if recipe.get("eCode", {}).get("enabled") else 0
capture_kinds = [prop(p, "kind") for kind, p in writes if kind == "CaptureIntent"]
frozen = [p for _, p in writes if prop(p, "kind") == "RecipeExecutionDeadlinesFrozen"]
media = list(con.execute("select RelativeKey,ByteLength,State from Media where lower(RunId)=?", (run_id,)))
media_checks = []
media_readback = []
for key, length, state in media:
    file = media_path(store / key)
    if not file.exists():
        file = media_path(store / "media-root" / key)
    digest = hashlib.sha256(file.read_bytes()).hexdigest().upper() if file.exists() else None
    saved = [p for kind, p in writes if kind == "Media" and prop(p, "relativeKey") == key]
    saved_hashes = [prop(p, "sha256") for _, _, _, p in events if prop(p, "relativeKey") == key and prop(p, "sha256")]
    media_checks.append(file.exists() and file.stat().st_size == length and state == "FileCompleted"
                        and len(saved) == 1 and all(h.upper() == digest for h in saved_hashes))
    media_readback.append({"relativeKey": key, "byteLength": length, "sha256": digest})
def stage_exists(stage, kind):
    return any(s == stage and k == kind and not error for s, k, error, _ in events)
checks = {
    "same_run_actual_page_final": page.get("outcome") == "FinalPageDisplayed" and page["receipt"]["body"]["runId"].lower() == run_id and facts["run"]["state"] == 26 and facts["run"]["finalOutcome"] == 1 and not facts["run"].get("errorCode"),
    "catalog_and_frozen_version": catalog_digest == fixture["recipeRef"]["catalogDigest"] and len(frozen) == 1 and prop(frozen[0], "catalogDigest") == catalog_digest and prop(frozen[0], "recipeVersion") == fixture["recipeRef"]["version"],
    "full_camera_entity_face_sequence": actual == expected,
    "one_initial_3d_f_no_rescan": capture_kinds.count("Capture3D") == capture_kinds.count("CaptureF") == 1 and "RescanWholeTray" not in capture_kinds,
    "actual_media_readback": len(media) == 2 + len(expected) + e_count and all(media_checks),
    "independent_worker_and_fusion_calls": sum(kind == "AlgorithmIntent" for kind, _ in writes) == 2 + len(expected) + face_count + e_count,
    "complete_tail": all(stage_exists(s, k) for s, k in [("Detection", "Completed"), ("UnloadPreparation", "Completed"), ("Sorting", "Completed"), ("UnlockObservation", "ObservedUnlocked"), ("ManualTrayRemovalConfirmation", "FinalUnloadCompleted")]),
}
notifications = [entry.get('envelope') for entry in page.get('notifications', [])
                 if (entry.get('runId') or '').lower() == run_id]
checks['actual_semantic_status_and_notifications'] = bool(
    facts['status'].get('schemaVersion') == 's01-status/2.0'
    and facts['status']['plc']['schemaVersion'] == 'device-semantics/1'
    and not page.get('notificationReadErrors')
    and notifications and all(isinstance(event, dict)
        and event.get('schemaVersion') == 's01/notification/2.0'
        and isinstance(event.get('summary'), dict)
        and set(event['summary']) == {'executionState', 'wholeTaskState', 'errorCode'}
        and isinstance(event['summary']['executionState'], str)
        and isinstance(event['summary']['wholeTaskState'], str)
        and (event['summary']['errorCode'] is None or isinstance(event['summary']['errorCode'], str))
        for event in notifications))
# Current committed business facts, never reconstructed protocol phases.
committed_sorting = [(event_id, json.loads(payload)) for event_id, payload in con.execute(
    "select EventId,PayloadJson from StageEvents where lower(RunId)=? and Stage='Sorting' order by Sequence", (run_id,))]
transfers = sorting_commits(committed_sorting, run_id)
checks["committed_pick_before_complete_transfer"] = all(
    any(m.get("entityId") == transfer["entityId"] and m.get("committedEventId", "").lower() == transfer["placeEventId"].lower()
        and m.get("state") == "Completed" for m in facts["run"].get("movements", [])) for transfer in transfers)
checks["no_unknown_held_sorting"] = not any(s == "Sorting" and k == "UnknownHeld" for s, k, _, _ in events)
context = facts["run"].get("resultContext") or {}
current = next((r for r in facts["run"].get("results", []) if r.get("kind") == context.get("kind") and r.get("id") == context.get("id")), None)
before = page.get("resultBeforeRefresh") or {}
after = page.get("resultAfterRefresh") or {}
checks["real_quality_before_and_after_refresh"] = bool(current and current.get("availability") == "Committed"
    and before.get("verdict") == after.get("verdict") == current.get("disposition")
    and current.get("id") in (before.get("items") or "") and current.get("id") in (after.get("items") or ""))
reopened = page.get("resultAfterReopen") or {}
checks["page_reopen_current_quality"] = bool(current and reopened.get("verdict") == current.get("disposition") and current.get("id") in (reopened.get("items") or ""))
if fixture["caseId"] == "Q02-PENDING-P03":
    physical = [r for r in facts["run"]["results"] if r["kind"] == "Single"]
    p03 = [r for r in physical if r["id"].endswith(":P:P03:M01")]
    p01 = [r for r in physical if r["id"].endswith(":P:P01:M01")]
    movements = facts["run"].get("movements", [])
    occupied_ids = [event_id.lower() for event_id, payload in con.execute(
        "select EventId,PayloadJson from StageEvents where lower(RunId)=? and Stage='Sorting' and EventType='Completed'", (run_id,))
        if prop(json.loads(payload), "kind") == "SortingAssignmentOccupied"]
    checks["p03_pending_reliable_committed_disposition"] = bool(len(p03) == 1 and p03[0]["disposition"] == "Pending"
        and p03[0].get("dispositionState") == "Completed" and len(movements) == 1
        and movements[0]["entityId"] == p03[0]["id"] and movements[0]["physicalSlotIndex"] == 3
        and movements[0]["sourcePointRef"] == "P03" and movements[0]["targetPointRef"] == "P15"
        and movements[0]["state"] == "Completed" and movements[0]["committedEventId"].lower() in occupied_ids)
    checks["p01_ok_explicitly_retained_without_face_movement"] = bool(len(p01) == 1 and p01[0]["disposition"] == "OK"
        and p01[0].get("dispositionState") == "NoMoveRequired"
        and all(r.get("dispositionState") is None for r in facts["run"]["results"] if r["kind"] == "Face"))
    checks["actual_page_disposition_refresh_reopen"] = bool(p03 and all(
        p03[0]["id"] in (value.get("items") or "") and "处置 Completed" in (value.get("items") or "")
        and value.get("verdict") == "Pending" for value in (before, after, reopened)))
if page.get("recipeHeaderCheck"):
    ref = facts["run"].get("recipeExecution") or facts["run"].get("recipeSelection") or {}
    checks["authoritative_recipe_header_refresh_reopen"] = bool(ref.get("recipeId") and ref.get("version") and all(
        ref["recipeId"] in (value.get("recipeName") or "") and value.get("recipeVersion") == ref["version"]
        for value in (before, after, reopened)))
if fixture["caseId"] == "Q01-PAUSE":
    pause = page.get("normalPause") or {}
    kinds = [prop(p, "kind") for _, p in writes] + [prop(p, "kind") for _, _, _, p in events]
    checks["normal_pause_check_continue_same_run"] = (pause.get("runId", "").lower() == run_id
        and pause.get("pausedState") == "Paused" and bool(pause.get("checkId"))
        and kinds.count("NormalPaused") == kinds.count("NormalContinued") == 1
        and not page.get("faultReceipt"))
if fixture["caseId"] == "RECOVERY-F":
    old_run = (page.get("faultReceipt", {}).get("body") or {}).get("runId", "").lower()
    proofs = [page.get(name) or {} for name in ("oldMediaBeforeFault", "oldMediaAtFault", "oldMediaAfterReset")]
    fingerprints = [sorted((x["mediaId"], x["sha256"], x["byteLength"]) for x in proof.get("items", [])) for proof in proofs]
    checks["old_actual_media_visible_through_fault_and_reset"] = bool(old_run and old_run != run_id
        and all(proof.get("runId", "").lower() == old_run and proof.get("items")
                and all(x.get("displayed") for x in proof["items"]) for proof in proofs)
        and fingerprints[0] == fingerprints[1] == fingerprints[2])
    old_media = list(con.execute("select MediaId,RelativeKey,ByteLength,State from Media where lower(RunId)=?", (old_run,)))
    old_readback = []
    for media_id, key, length, state in old_media:
        file = media_path(store / key)
        if not file.exists(): file = media_path(store / "media-root" / key)
        digest = hashlib.sha256(file.read_bytes()).hexdigest().upper() if file.exists() else None
        old_readback.append((media_id.lower(), digest, length))
    checks["old_media_sqlite_file_api_same_run"] = bool(old_media and all(state == "FileCompleted" for _, _, _, state in old_media)
        and sorted(old_readback) == sorted((mid.lower(), sha, length) for mid, sha, length in fingerprints[0]))
    actions = [x["action"] for x in page["interactions"]]
    checks["fault_reset_initial_check_explicit_new_run"] = ("reset-device-on-page" in actions
        and "verify-initial-state-on-page" in actions and "explicit-new-run-start-on-page" in actions
        and (facts.get("faultRun") or {}).get("runId", "").lower() == old_run)
if len({r.get("id") for r in facts["run"].get("results", []) if r.get("kind") in ("Single", "Member", "Part")}) > 1:
    switched = page.get("objectSwitch") or {}
    chosen = next((r for r in facts["run"].get("results", []) if r.get("kind") in ("Single", "Member", "Part") and r.get("id") == switched.get("objectId")), None)
    checks["existing_object_switch_quality"] = bool(chosen and switched.get("page", {}).get("verdict") == chosen.get("disposition") and chosen.get("id") in (switched.get("page", {}).get("items") or ""))
checks["real_inspection_media_identity"] = bool(current and current.get("inspections") and all(
    all(str(media_id).lower() in {str(prop(p, "mediaId")).lower() for _, _, _, p in events if prop(p, "mediaId")} for media_id in inspection.get("mediaIds", []))
    for inspection in current.get("inspections", [])))
checks["missing_detail_not_demonstration"] = bool(current and all(
    i.get("confidence") is None and i.get("defects") is None for i in current.get("inspections", [])
    if i.get("detailAvailability", {}).get("confidence") == "NotProvided" and i.get("detailAvailability", {}).get("defects") == "NotProvided"))
resource_log = root / "logs" / "desktop-runtime.jsonl"
resolved = [load_value for line in resource_log.read_text(encoding="utf-8-sig").splitlines()
            for load_value in [json.loads(line)] if load_value.get("kind") == "ResourcesResolved"] if resource_log.exists() else []
loaded = load(root / "interactive-desktop.json")
checks["actual_wpf_loaded_resource"] = bool(resolved and all(
    r.get("facts", {}).get("runtimeSha256") == loaded.get("runtimeSha256") and r.get("facts", {}).get("resourceRoot") for r in resolved))
if facts.get("faultRun"):
    old_id = facts["faultRun"]["runId"].lower()
    checks["restart_new_identity_old_fault_retained"] = (old_id != run_id and facts["faultRun"].get("finalOutcome") == 0
        and facts["faultRun"].get("faultRestart", {}).get("newRunId", "").lower() == run_id
        and facts["run"].get("faultRestart", {}).get("faultRunId", "").lower() == old_id)
    checks["restart_page_explicit_operations"] = all(any(x.get("action") == action for x in page.get("interactions", []))
        for action in ["reset-device-on-page", "verify-initial-state-on-page", "explicit-new-run-start-on-page", "confirm-tray-removal-on-page"])
if special:
    actions = [prop(payload, "result", {}) for _, _, _, payload in events
               if prop(payload, "kind") in ("SpecialActionConfirmed", "SpecialExitCompleted")]
    exits = [a for a in actions if prop(prop(a, "request", {}), "kind") == "Exit"]
    checks["actual_special_actions_and_free_exit"] = (
        len(actions) == len(positions) * (2 + len(stages)) and len(exits) == len(positions)
        and all(prop(prop(a, "evidence", {}), "meaning") == "MaterialTransferred"
                and prop(prop(a, "evidence", {}), "isCorrelated") is True
                and prop(prop(a, "evidence", {}), "correlation") == prop(prop(a, "request", {}), "correlation")
                and prop(prop(a, "evidence", {}), "diagnosticEvidenceReferences") for a in actions)
        and all(not prop(a, "occupiedEntityId") for a in exits))
result = {"source": "Test/actual-WPF-WebView2/VirtualPlc", "runId": run_id,
          "recipeRef": fixture["recipeRef"], "expectedCameras": expected, "actualCameras": actual,
          "mediaReadback": media_readback, "checks": checks, "passed": all(checks.values()), "productionVerified": False}
with (root / "operation-route-validation.json").open("x", encoding="utf-8") as output:
    json.dump(result, output, ensure_ascii=False, indent=2)
print(json.dumps({"runId": run_id, "checks": checks}, ensure_ascii=True))
raise SystemExit(0 if result["passed"] else 1)
