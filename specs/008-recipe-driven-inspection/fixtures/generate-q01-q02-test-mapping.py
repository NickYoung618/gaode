"""Versioned, virtual-only Q01/Q02 fixture update; no production coordinates."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parent
version = "1.1.1-test"
mapping_version = "test-virtual-mapping/1.0.1"

for case, suffix, cameras, slots, code in (
    ("Q01", "", "AB", ("P01",), "TEST-TRAY-0101"),
    ("Q02", "-q02", "CD", ("P01", "P03"), "TEST-TRAY-0202"),
):
    recipe_path = root / f"recipes{suffix}.json"
    catalog = json.loads(recipe_path.read_text(encoding="utf-8"))
    catalog["schemaVersion"] = "review-recipe-catalog/0.5"
    recipe = catalog["recipes"][0]
    recipe["version"] = version
    recipe["testEligibleSlots"] = list(slots)
    catalog["source"]["status"] = "TestOnly/VirtualMapped"
    catalog["source"]["pointMapping"] = mapping_version
    catalog["source"]["heightMapping"] = mapping_version
    for position in recipe["positions"]:
        slot = position["slotId"]
        if slot not in slots:
            continue
        index = int(slot[1:])
        sample = "sample-a" if slot == "P01" else "sample-b"
        center_x = 100 if slot == "P01" else 300
        center_y = 100
        position["protocolSlotIndex"] = index
        position["pointRefs"] = {camera: f"{case}-{slot}-{camera}-{mapping_version}"
                                 for camera in cameras}
        position["resolvedSourcePoint"] = {
            "id": slot, "version": mapping_version,
            "x": center_x, "y": center_y, "z": 150,
            "unit": "mm", "frame": "SIM_MACHINE",
            "testSourceRef": f"Test/VirtualSource/{case}/{slot}/{mapping_version}",
            "point": {"id": slot, "version": mapping_version,
                      "x": center_x, "y": center_y, "z": 150,
                      "unit": "mm", "frame": "SIM_MACHINE"},
        }
        targets = {}
        for camera in cameras:
            dx, dy = {"A": (0, 0), "B": (10, 0),
                      "C": (0, 10), "D": (10, 10)}[camera]
            targets[camera] = {
                "pointRef": position["pointRefs"][camera],
                "zMode": "TestHeightOffset",
                "testSourceRef": f"Test/VirtualPoint/{case}/{slot}/{camera}/{mapping_version}",
                "point": {"id": f"{case}-{slot}-{camera}", "version": mapping_version,
                          "x": center_x + dx, "y": center_y + dy,
                          "unit": "mm", "frame": "SIM_MACHINE"},
                "heightBinding": {"scopeId": "sim-whole-tray", "scopeVersion": "1.0.0",
                                  "sampleId": sample, "objectSlotId": slot,
                                  "localFace": 1, "heightRound": 1,
                                  "unit": "mm", "datum": "SIM_REFERENCE",
                                  "offsetMm": 100.0, "minZ": 105.0, "maxZ": 120.0},
            }
        position["resolvedDetectionTargets"] = targets
    recipe_path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    digest = hashlib.sha256(recipe_path.read_bytes()).hexdigest().upper()
    fixture_path = root / f"fixture{suffix}.json"
    fixture = json.loads(fixture_path.read_text(encoding="utf-8"))
    fixture["recipeRef"]["version"] = version
    fixture["recipeRef"]["catalogDigest"] = digest
    fixture["availability"] = "Available"
    fixture["restrictions"] = ["TestVirtualOnly;ProductionCalibrationNotApproved"]
    fixture_path.write_text(json.dumps(fixture, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    worker_path = root / f"worker-manifest{suffix}.json"
    worker = json.loads(worker_path.read_text(encoding="utf-8"))
    assert worker["fCode"] == code
    worker["version"] = version
    worker["detectionDisposition"] = ["OK"]
    worker_path.write_text(json.dumps(worker, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    cases_path = root / "cases.json"
    cases = json.loads(cases_path.read_text(encoding="utf-8"))
    row = next(row for row in cases["cases"] if row["caseId"] == case)
    row["recipeRef"]["version"] = version
    row["recipeRef"]["catalogDigest"] = digest
    row["availability"] = "Available"
    row["restrictions"] = ["TestVirtualOnly;ProductionCalibrationNotApproved"]
    for object_row in ([row["object"]] if case == "Q01" else row["objects"]):
        slot = object_row["sourceSlotId"]
        position = next(p for p in recipe["positions"] if p["slotId"] == slot)
        object_row["protocolSlotIndex"] = position["protocolSlotIndex"]
        object_row["pointRefs"] = position["pointRefs"]
        object_row["heightBinding"] = {
            "scopeId": "sim-whole-tray", "scopeVersion": "1.0.0",
            "sampleId": "sample-a" if slot == "P01" else "sample-b",
            "objectSlotId": slot, "localFace": 1, "heightRound": 1,
            "unit": "mm", "datum": "SIM_REFERENCE", "offsetMm": 100.0,
            "minZ": 105.0, "maxZ": 120.0,
        }
    cases_path.write_text(json.dumps(cases, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{case} {version} {digest}")
