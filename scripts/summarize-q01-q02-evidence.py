"""Read back the two completed Test runs; never starts business actions."""
import json
import re
import sqlite3
import sys
from pathlib import Path

cases = [("Q01", Path(sys.argv[1]), ["A:P01", "B:P01"], 2, 1),
         ("Q02", Path(sys.argv[2]), ["C:P01", "C:P03", "D:P01", "D:P03"], 4, 2)]
if len(sys.argv) == 4:
    cases.append(("Q01-PARAM", Path(sys.argv[3]),
                  ["A:P01", "A:P03", "B:P01", "B:P03"], 4, 2))
elif len(sys.argv) != 3:
    raise SystemExit("usage: verifier Q01-root Q02-root [Q01-PARAM-root]")
summaries = []
for case, root, expected_order, captures, fusions in cases:
    page = json.loads((root / "recipe-webview2-page-evidence.json").read_text(encoding="utf-8-sig"))
    api = json.loads((root / "page-api-device-facts.json").read_text(encoding="utf-8-sig"))
    process = json.loads((root / "process.json").read_text(encoding="utf-8-sig"))
    fixture = json.loads((Path(__file__).resolve().parents[1] / "specs/008-recipe-driven-inspection/fixtures" /
                          ({"Q01": "fixture.json", "Q02": "fixture-q02.json",
                            "Q01-PARAM": "fixture-q01-param.json"}[case])).read_text(encoding="utf-8"))
    db = sqlite3.connect(f"file:{root / 'station01.test.db'}?mode=ro", uri=True)
    run_id = page["receipt"]["body"]["runId"]
    assert page["receipt"]["status"] == 202
    assert api["runId"].lower() == run_id.lower()
    assert api["run"]["wholeTaskState"] == api["evidence"]["finalResult"] == "FinalUnloadCompletion"
    assert page["states"][-1]["verdict"] == "完成"
    assert len([x for x in page["interactions"] if x["action"] == "confirm-tray-removal-on-page"]) == 1
    assert process["configuration"]["recipeCatalogSha256"] == fixture["recipeRef"]["catalogDigest"]
    writes = [(kind, json.loads(payload)) for kind, payload in db.execute(
        "select Kind,PayloadJson from Writes order by Revision")]
    selected = api["run"]["recipeSelection"]
    executed = api["run"]["recipeExecution"]
    for ref in (selected, executed):
        assert ref["recipeId"] == fixture["recipeRef"]["recipeId"]
        assert ref["version"] == fixture["recipeRef"]["version"]
        assert ref["catalogDigest"] == fixture["recipeRef"]["catalogDigest"]
    assert page["recipeSelection"]["version"] == fixture["recipeRef"]["version"]
    assert any(kind == "AlgorithmFact" and fixture["fCode"] in str(x.get("rawResultJson", ""))
               for kind, x in writes)
    bound = [x for kind, x in writes if kind == "ActionFact" and x.get("kind") == "RecipePlanBound"]
    assert len(bound) == 1
    assert bound[0]["recipeVersion"] == fixture["recipeRef"]["version"]
    assert bound[0]["catalogDigest"] == fixture["recipeRef"]["catalogDigest"]
    assert bound[0]["planRevision"] == executed["planRevision"]
    height = next(json.loads(x.get("rawResultJson") or x.get("RawResultJson"))
                  for kind, x in writes if kind == "AlgorithmFact" and
                  (x.get("rawResultJson") or x.get("RawResultJson", "")).startswith("[" ) and
                  "sample-a" in (x.get("rawResultJson") or x.get("RawResultJson", "")))
    samples = {x["SourceElementId"]: x["RawValue"] for x in height}
    moves = [x.get("TargetOrScope") or x.get("targetOrScope") for kind, x in writes
             if kind == "ActionIntent" and (x.get("Kind") or x.get("kind")) == "Detection"]
    parsed = []
    for move in moves:
        match = re.search(r"Q\d+(?:-PARAM)?-(P\d+)-([ABCD]).*?Z=([\d.]+);Test/MeasuredHeightOffset;.*?:(sample-[ab]):", move)
        assert match, move
        slot, camera, z_text, sample_id = match.groups()
        assert abs(float(z_text) - (samples[sample_id] + 100.0)) < 0.00001
        assert sample_id == ("sample-a" if slot == "P01" else "sample-b")
        parsed.append({"camera": camera, "slot": slot, "sampleId": sample_id,
                       "heightMm": samples[sample_id], "targetZmm": float(z_text)})
    assert [f"{x['camera']}:{x['slot']}" for x in parsed] == expected_order
    stages = [(stage, event, json.loads(payload)) for stage, event, payload in db.execute(
        "select Stage,EventType,PayloadJson from StageEvents")]
    release_count = sum(1 for _, _, payload in stages if payload.get("kind") == "AcquisitionReleased")
    fusion_count = sum(1 for _, _, payload in stages if payload.get("kind") == "FaceFusionCommitted")
    assert release_count == captures and fusion_count == fusions
    assert all(any(s == stage and e == event for s, e, _ in stages) for stage, event in
               (("Detection", "Completed"), ("Sorting", "Completed"),
                ("UnloadPreparation", "Completed"), ("UnlockObservation", "ObservedUnlocked"),
                ("ManualTrayRemovalConfirmation", "ManualTrayRemovalConfirmed"),
                ("ManualTrayRemovalConfirmation", "FinalUnloadCompleted")))
    media = list(db.execute("select RelativeKey,ByteLength from Media"))
    assert len(media) == captures + 2
    assert all((root / "media-root" / key).is_file() and
               (root / "media-root" / key).stat().st_size == length for key, length in media)
    worker = [json.loads(line) for line in (root / "media-root/worker-protocol.jsonl").read_text().splitlines()]
    assert len([x for x in worker if x["event"] == "Result" and x.get("role") == "Detection"]) == captures + fusions
    summary = {"caseId": case, "runId": run_id, "source": "Test/VirtualPlc-WPF",
               "pageFinal": True, "apiFinal": True, "catalogDigest": fixture["recipeRef"]["catalogDigest"],
               "recipeVersion": fixture["recipeRef"]["version"], "heightSamples": samples,
               "detectionMoves": parsed, "acquisitionReleaseCount": release_count, "fusionCount": fusion_count,
               "mediaReadBackCount": len(media), "workerResultCount": captures + fusions,
               "manualPageClicks": 1, "communicationExpectation": {"captures": captures}}
    with (root / "verified-facts.json").open("x", encoding="utf-8") as output:
        json.dump(summary, output, ensure_ascii=False, indent=2)
    summaries.append(summary)
assert summaries[0]["heightSamples"]["sample-a"] != summaries[1]["heightSamples"]["sample-a"]
assert summaries[0]["detectionMoves"][0]["targetZmm"] != summaries[1]["detectionMoves"][0]["targetZmm"]
print(json.dumps(summaries, ensure_ascii=False, indent=2))

if len(summaries) == 3:
    baseline, _, changed = summaries
    old_root, new_root = Path(sys.argv[1]), Path(sys.argv[3])
    old_process = json.loads((old_root / "process.json").read_text(encoding="utf-8-sig"))
    new_process = json.loads((new_root / "process.json").read_text(encoding="utf-8-sig"))
    assert old_process["configuration"]["hostDllSha256"] == new_process["configuration"]["hostDllSha256"]
    assert old_process["configuration"]["plcDllSha256"] == new_process["configuration"]["plcDllSha256"]
    assert baseline["catalogDigest"] != changed["catalogDigest"]
    old_desktop = json.loads((old_root / "interactive-desktop.json").read_text(encoding="utf-8-sig"))
    new_desktop = json.loads((new_root / "interactive-desktop.json").read_text(encoding="utf-8-sig"))
    assert old_desktop["runtimeSha256"] == new_desktop["runtimeSha256"]
    def frozen_catalog_digest(root):
        db = sqlite3.connect(f"file:{root / 'station01.test.db'}?mode=ro", uri=True)
        facts = [json.loads(payload) for (payload,) in db.execute(
            "select PayloadJson from Writes where Kind='Audit'")
            if 'RecipeExecutionDeadlinesFrozen' in payload]
        assert len(facts) == 1
        return facts[0]["catalogDigest"]
    assert frozen_catalog_digest(old_root) == baseline["catalogDigest"]
    assert frozen_catalog_digest(new_root) == changed["catalogDigest"]
    def capture_settings(root):
        settings = []
        for line in (root / "logs/host.out.log").read_text(encoding="utf-8", errors="replace").splitlines():
            if 'RuntimeFlow {' not in line or '"step":"DetectionCapture"' not in line or '"outcome":"Requesting"' not in line:
                continue
            event = json.loads(line[line.index('{'):])
            settings.append(event["facts"]["settings"])
        return settings
    old_settings, new_settings = capture_settings(old_root), capture_settings(new_root)
    assert len(old_settings) == 2 and len(new_settings) == 4
    assert all(x["ExposureUs"] == 10000 and x["BrightnessPercent"] == 60 and
               x["RoiPixels"] == [0,0,1024,1024] for x in old_settings)
    assert all(x["ExposureUs"] == 12000 and x["BrightnessPercent"] == 75 and
               x["RoiPixels"] == [0,0,960,960] for x in new_settings)
    def detection_parameters(root):
        db = sqlite3.connect(f"file:{root / 'station01.test.db'}?mode=ro", uri=True)
        values = [json.loads(payload).get("ParametersVersion") for (payload,) in db.execute(
            "select PayloadJson from Writes where Kind='AlgorithmIntent'")]
        return [x for x in values if x and x.startswith("SIM_ALGORITHM")]
    assert detection_parameters(old_root) == ["SIM_ALGORITHM"] * 3
    assert detection_parameters(new_root) == ["SIM_ALGORITHM_PARAM"] * 6
    report = {"caseId": "Q01-PARAM", "result": "TestVirtualEndToEndPassed",
              "baselineRunId": baseline["runId"], "newRunId": changed["runId"],
              "sameHostSha256": old_process["configuration"]["hostDllSha256"],
              "samePlcSha256": old_process["configuration"]["plcDllSha256"],
              "sameFrontendRuntimeSha256": old_desktop["runtimeSha256"],
              "baselineCatalogDigest": baseline["catalogDigest"],
              "newCatalogDigest": changed["catalogDigest"],
              "baselineOrder": [f"{x['camera']}:{x['slot']}" for x in baseline["detectionMoves"]],
              "newOrder": [f"{x['camera']}:{x['slot']}" for x in changed["detectionMoves"]],
              "baselineCaptureSettings": old_settings[0],
              "newCaptureSettings": new_settings[0],
              "baselineAlgorithmProfile": "SIM_ALGORITHM",
              "newAlgorithmProfile": "SIM_ALGORITHM_PARAM",
              "oldWorkerSeed": 7001, "newWorkerSeed": 7031,
              "oldSnapshotCatalogDigestPreserved": True,
              "source": "Test/VirtualPlc-WPF;notProductionCalibration"}
    (new_root / "config-change-verified.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))
