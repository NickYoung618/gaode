"""Prepare the Q03 virtual two-face fixture with an explicit flip position."""

import copy
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parent


def read(name):
    return json.loads((root / name).read_text(encoding="utf-8"))


def write(name, value):
    data = json.dumps(value, ensure_ascii=False, indent=2) + "\n"
    encoded = data.encode("utf-8")
    (root / name).write_bytes(encoded)
    return hashlib.sha256(encoded).hexdigest().upper()


version = "1.0.0-test"
catalog = read("recipes.json")
recipe = copy.deepcopy(catalog["recipes"][0])
recipe["recipeId"] = "R008-Q03"
recipe["version"] = version
recipe["model"] = "Q03-Test-TwoFace"
recipe["fCode"]["virtualExactPayload"] = "TEST-TRAY-0103"
recipe["plcRecipeId"] = 103
recipe["compositionPerOccupiedSlot"][0]["localFaces"] = [1, 2]
recipe["execution"]["stages"].append({
    "stage": 2,
    "action": "flipAffectedMembersOneByOne",
    "targets": [{"material": "BASE", "localFace": 2, "cameraPair": "AB",
                 "captureProfile": "SIM_CAPTURE_AB", "algorithmProfile": "SIM_ALGORITHM"}],
    "order": "faceThenCameraThenAllOccupiedSlots",
    "coordinateRule": "initial3D;perFaceTargets;retainIdentity",
})
recipe["testEligibleSlots"] = ["P01"]
for position in recipe["positions"]:
    if position["slotId"] != "P01":
        continue
    position["resolvedSourcePoint"]["version"] = "test-virtual-mapping/q03-1.0.0"
    position["resolvedSourcePoint"]["testSourceRef"] = "Test/VirtualSource/Q03/P01/1.0.0"
    position["resolvedSourcePoint"]["point"]["version"] = "test-virtual-mapping/q03-1.0.0"
    flip_position = copy.deepcopy(position["resolvedSourcePoint"])
    flip_position["testSourceRef"] = "Test/VirtualFlipPosition/Q03/P01/1.0.0"
    flip_position["point"]["id"] = "Q03-P01-flip-position"
    position["resolvedFlipPosition"] = flip_position
    position["resolvedSortingTargets"] = {
        disposition: {
            "testSourceRef": f"Test/VirtualSameTray/{disposition}/P01/1.0.0",
            "targetSlotId": slot,
            "point": {"id": slot, "version": "test-virtual-mapping/q03-1.0.0",
                      "x": x, "y": 300, "z": 150,
                      "unit": "mm", "frame": "SIM_MACHINE"},
        }
        for disposition, slot, x in (("NG", "P14", 400), ("Pending", "P15", 500))
    }
    first = copy.deepcopy(position["resolvedDetectionTargets"])
    for camera, item in first.items():
        item["pointRef"] = f"Q03-P01-{camera}-test-virtual-mapping/1.0.0"
        item["point"]["id"] = f"Q03-P01-face1-{camera}"
        item["point"]["version"] = "test-virtual-mapping/q03-1.0.0"
        item["testSourceRef"] = f"Test/VirtualPoint/Q03/P01/face1/{camera}/1.0.0"
        item["zMode"] = "InitialMeasurementOffset"
    second = copy.deepcopy(first)
    for camera, item in second.items():
        item["point"]["id"] = f"Q03-P01-face2-{camera}"
        item["point"]["version"] = "test-virtual-mapping/q03-1.0.0"
        item["point"]["x"] = 120 if camera == "A" else 130
        item["testSourceRef"] = f"Test/VirtualPoint/Q03/P01/face2/{camera}/1.0.0"
        item["heightBinding"]["localFace"] = 2
        item["heightBinding"]["heightRound"] = 1
        item["heightBinding"]["scopeVersion"] = "1.0.0"
        item["heightBinding"]["sampleId"] = "sample-a"
        item["heightBinding"]["offsetMm"] = 102.0
    modern = {}
    for face, cameras in ((1, first), (2, second)):
        modern[str(face)] = {}
        for camera, item in cameras.items():
            binding = item["heightBinding"]
            modern[str(face)][camera] = {
                "executionStageId": f"stage:{face}",
                "objectId": "{UnitId}:M01",
                "sourceSlotId": position["slotId"],
                "protocolSlotIndex": position["protocolSlotIndex"],
                "localFace": face,
                "cameraId": camera,
                "pointRef": item["pointRef"],
                "point": {**item["point"], "testSourceRef": item["testSourceRef"]},
                "coordinateConfigVersion": "test-virtual-mapping/q03-1.0.0",
                "zBasis": {
                    "mode": "InitialMeasurementOffset",
                    "measurementRef": {key: binding[key] for key in
                        ("scopeId", "scopeVersion", "sampleId", "objectSlotId", "unit", "datum")},
                    "offsetMm": binding["offsetMm"], "minZ": binding["minZ"],
                    "maxZ": binding["maxZ"],
                    "mappingSourceRef": item["testSourceRef"],
                },
            }
    position["resolvedDetectionTargetsByFace"] = modern
    position["pointRefs"] = {"A": first["A"]["pointRef"], "B": first["B"]["pointRef"]}
    position.pop("resolvedDetectionTargets")
catalog["recipes"] = [recipe]
catalog["schemaVersion"] = "review-recipe-catalog/0.5"
catalog["source"]["status"] = "TestOnly/ExplicitFlipPosition"
catalog["source"]["pointMapping"] = "test-virtual-mapping/q03-1.0.0"
catalog["source"]["heightMapping"] = "test-virtual-mapping/q03-1.0.0"
digest = write("recipes-q03.json", catalog)

worker = read("worker-manifest.json")
worker.update(id="q03-virtual-algorithm", version=version, fCode="TEST-TRAY-0103", seed=7033)
write("worker-manifest-q03.json", worker)
media = read("media-manifest.json")
media.update(id="q03-simulated-media", version=version)
write("media-manifest-q03.json", media)
fixture = read("fixture.json")
fixture["caseId"] = "Q03"
fixture["occupiedSlots"] = ["P01"]
fixture["fCode"] = "TEST-TRAY-0103"
fixture["availability"] = "Available"
fixture["recipeRef"].update(recipeId="R008-Q03", version=version, catalogDigest=digest)
fixture["recipeCatalogPath"] = str((root / "recipes-q03.json").resolve())
fixture["imageManifestPath"] = str((root / "media-manifest-q03.json").resolve())
fixture["workerManifestPath"] = str((root / "worker-manifest-q03.json").resolve())
fixture["restrictions"] = []
write("fixture-q03.json", fixture)
ng_worker = copy.deepcopy(worker)
ng_worker.update(id="q03-ng-virtual-algorithm", detectionDisposition=["NG"])
write("worker-manifest-q03-ng.json", ng_worker)
ng_fixture = copy.deepcopy(fixture)
ng_fixture["caseId"] = "Q03-NG"
ng_fixture["workerManifestPath"] = str((root / "worker-manifest-q03-ng.json").resolve())
write("fixture-q03-ng.json", ng_fixture)
pending_worker = copy.deepcopy(worker)
pending_worker.update(id="q03-pending-virtual-algorithm", detectionDisposition=["Pending"])
write("worker-manifest-q03-pending.json", pending_worker)
pending_fixture = copy.deepcopy(fixture)
pending_fixture["caseId"] = "Q03-Pending"
pending_fixture["workerManifestPath"] = str((root / "worker-manifest-q03-pending.json").resolve())
write("fixture-q03-pending.json", pending_fixture)

# A second Test-only occupied slot exercises consecutive physical entities
# targeting the same next face without changing the single-slot page fixture.
two_slot_catalog = copy.deepcopy(catalog)
two_slot_recipe = two_slot_catalog["recipes"][0]
two_slot_recipe["testEligibleSlots"] = ["P01", "P02"]
first_position = next(p for p in two_slot_recipe["positions"] if p["slotId"] == "P01")
second_position = next(p for p in two_slot_recipe["positions"] if p["slotId"] == "P02")
second_position["protocolSlotIndex"] = 2
for key in ("resolvedSourcePoint", "resolvedFlipPosition", "resolvedDetectionTargetsByFace"):
    second_position[key] = copy.deepcopy(first_position[key])
second_position["pointRefs"] = {
    camera: value.replace("P01", "P02") for camera, value in first_position["pointRefs"].items()
}
second_position.pop("resolvedDetectionTargets", None)
for key in ("resolvedSourcePoint", "resolvedFlipPosition"):
    value = second_position[key]
    value["id"] = value["id"].replace("P01", "P02")
    value["x"] += 100
    value["testSourceRef"] = value["testSourceRef"].replace("P01", "P02")
    value["point"]["id"] = value["point"]["id"].replace("P01", "P02")
    value["point"]["x"] += 100
for face_targets in second_position["resolvedDetectionTargetsByFace"].values():
    for camera, target in face_targets.items():
        target["objectId"] = "{UnitId}:M01"
        target["sourceSlotId"] = "P02"
        target["protocolSlotIndex"] = second_position["protocolSlotIndex"]
        target["pointRef"] = target["pointRef"].replace("P01", "P02")
        target["point"]["id"] = target["point"]["id"].replace("P01", "P02")
        target["point"]["x"] += 100
        target["point"]["testSourceRef"] = target["point"]["testSourceRef"].replace("P01", "P02")
        target["zBasis"]["measurementRef"]["objectSlotId"] = "P02"
        target["zBasis"]["mappingSourceRef"] = target["zBasis"]["mappingSourceRef"].replace("P01", "P02")
two_slot_digest = write("recipes-q03-two-slot.json", two_slot_catalog)
two_slot_fixture = copy.deepcopy(fixture)
two_slot_fixture["caseId"] = "Q03-TwoSlot"
two_slot_fixture["occupiedSlots"] = ["P01", "P02"]
two_slot_fixture["recipeRef"]["catalogDigest"] = two_slot_digest
two_slot_fixture["recipeCatalogPath"] = str((root / "recipes-q03-two-slot.json").resolve())
write("fixture-q03-two-slot.json", two_slot_fixture)
print(json.dumps({"recipeId": "R008-Q03", "version": version,
                  "catalogDigest": digest, "availability": "Available"}))
