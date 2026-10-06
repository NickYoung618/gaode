"""Generate isolated virtual-only Q04-Q22 recipes from the frozen face matrix.

The XY offsets come from SIM_TRAY_5X3; per-face offsets and initial-height
bindings are versioned Test configuration, not machine calibration.
"""

import copy
import hashlib
import json
import re
import argparse
import shutil
import os
from pathlib import Path

source_root = Path(__file__).resolve().parent
feature = source_root.parent
parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, required=True, help='New, versioned virtual-only package directory')
root = parser.parse_args().output.resolve()
if root.exists():
    raise SystemExit('Output must be a new directory; historical packages are read-only')
root.mkdir(parents=True)
for original in source_root.glob('*.json'):
    shutil.copy2(original, root / original.name)
version = "1.0.2-test-usr-e"


def read(name):
    return json.loads((root / name).read_text(encoding="utf-8"))


def write(name, value):
    for image in value.get("images", []):
        relative = image.get("relativePath")
        if relative and not (root / relative).exists() and (source_root / relative).exists():
            image["relativePath"] = os.path.relpath((source_root / relative).resolve(), root).replace("\\", "/")
    data = (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    (root / name).write_bytes(data)
    return hashlib.sha256(data).hexdigest().upper()


matrix = {}
for line in (feature / "recipe-cases.md").read_text(encoding="utf-8").splitlines():
    match = re.match(r"\| (Q\d\d) \| R008-Q\d\d / 1\.0\.0-test \| (TEST-TRAY-\d{4}) \| ([A-D→]+) \| S3 \|", line)
    if match:
        case, fcode, route = match.groups()
        matrix[case] = (fcode, route.split("→"))
matrix = {case: value for case, value in matrix.items()
          if len(value[1]) == 2 or len(value[1]) == 4 and value[1].count('AB') in (1, 3)}
assert set(matrix) == {'Q03','Q04','Q05','Q06','Q08','Q09','Q11','Q14','Q15','Q18','Q20','Q21'}
assert len({item[0] for item in matrix.values()}) == len(matrix)

q03 = read("recipes-q03.json")
q02 = read("recipes-q02.json")
q03_media = read("media-manifest-q03.json")
q02_media = read("media-manifest-q02.json")
q03_worker = read("worker-manifest-q03.json")
q03_fixture = read("fixture-q03.json")
base_cases = read("cases.json")
base_cases["cases"] = [case for case in base_cases["cases"]
                       if case["caseId"] not in {f"Q{number:02d}" for number in range(3, 23)}]

for case in sorted(matrix):
    number = int(case[1:])
    fcode, pairs = matrix[case]
    if number == 3:
        catalog = q03
        digest = hashlib.sha256((root / "recipes-q03.json").read_bytes()).hexdigest().upper()
        media = q03_media
    else:
        catalog = copy.deepcopy(q03)
        catalog["source"] = {
            "basis": "recipe-cases.md Q03-Q22; SIM_TRAY_5X3 camera offsets; initial 3D sample-a",
            "status": "TestOnly/VirtualMultiFace",
            "pointMapping": f"test-virtual-mapping/{case.lower()}-1.0.0",
            "heightMapping": f"test-virtual-mapping/{case.lower()}-1.0.0",
        }
        for key in ("SIM_CAPTURE_CD", "SIM_ALGORITHM_CD"):
            catalog["virtualFixedProfiles"][key] = copy.deepcopy(q02["virtualFixedProfiles"][key])
        recipe = catalog["recipes"][0]
        recipe["recipeId"] = f"R008-{case}"
        recipe["version"] = version
        recipe["model"] = f"{case}-Test-{'-'.join(pairs)}"
        recipe["fCode"]["virtualExactPayload"] = fcode
        recipe["plcRecipeId"] = 100 + number
        recipe["compositionPerOccupiedSlot"][0]["localFaces"] = list(range(1, len(pairs) + 1))
        first_cd = pairs[0] == "CD"
        recipe["compositionPerOccupiedSlot"][0]["captureProfile"] = (
            "SIM_CAPTURE_CD" if first_cd else "SIM_CAPTURE_AB")
        recipe["compositionPerOccupiedSlot"][0]["algorithmProfile"] = (
            "SIM_ALGORITHM_CD" if first_cd else "SIM_ALGORITHM")
        recipe["execution"]["stages"] = [
            {"stage": face, "action": "none" if face == 1 else "flipAffectedMembersOneByOne",
             "targets": [{"material": "BASE", "localFace": face, "cameraPair": pair,
                          "captureProfile": "SIM_CAPTURE_CD" if pair == "CD" else "SIM_CAPTURE_AB",
                          "algorithmProfile": "SIM_ALGORITHM_CD" if pair == "CD" else "SIM_ALGORITHM"}],
             "order": "faceThenCameraThenAllOccupiedSlots",
             "coordinateRule": "initial3D" if face == 1 else "initial3D;perFaceTargets;retainIdentity"}
            for face, pair in enumerate(pairs, 1)
        ]
        point_version = f"test-virtual-mapping/{case.lower()}-1.0.0"
        position = next(p for p in recipe["positions"] if p["slotId"] == "P01")
        for key in ("resolvedSourcePoint", "resolvedFlipPosition"):
            value = position[key]
            value["version"] = point_version
            value["point"]["version"] = point_version
            value["testSourceRef"] = value["testSourceRef"].replace("Q03", case)
        for disposition, value in position["resolvedSortingTargets"].items():
            value["testSourceRef"] = value["testSourceRef"].replace("Q03", case)
            value["point"]["version"] = point_version
        offsets = catalog["virtualFixedProfiles"]["SIM_TRAY_5X3"]["cameraXYOffsetsMm"]
        cameras = sorted({camera for pair in pairs for camera in pair})
        position["pointRefs"] = {
            camera: f"{case}-P01-{camera}-test-virtual-mapping/1.0.0"
            for camera in cameras
        }
        position["resolvedDetectionTargetsByFace"] = {}
        for face, pair in enumerate(pairs, 1):
            face_targets = {}
            for camera in pair:
                source = f"Test/VirtualPoint/{case}/P01/face{face}/{camera}/1.0.0"
                dx, dy = offsets[camera]
                face_targets[camera] = {
                    "executionStageId": f"stage:{face}", "objectId": "{UnitId}:M01",
                    "sourceSlotId": "P01", "protocolSlotIndex": 1,
                    "localFace": face, "cameraId": camera,
                    "pointRef": position["pointRefs"][camera],
                    "point": {"id": f"{case}-P01-face{face}-{camera}",
                              "version": point_version, "x": 100 + 20 * (face - 1) + dx,
                              "y": 100 + dy, "unit": "mm", "frame": "SIM_MACHINE",
                              "testSourceRef": source},
                    "coordinateConfigVersion": point_version,
                    "zBasis": {"mode": "InitialMeasurementOffset",
                               "measurementRef": {"scopeId": "sim-whole-tray",
                                                  "scopeVersion": "1.0.0", "sampleId": "sample-a",
                                                  "objectSlotId": "P01", "unit": "mm",
                                                  "datum": "SIM_REFERENCE"},
                               "offsetMm": 100.0 + 2.0 * (face - 1),
                               "minZ": 105.0, "maxZ": 120.0,
                               "mappingSourceRef": source},
                }
            position["resolvedDetectionTargetsByFace"][str(face)] = face_targets
        media = copy.deepcopy(q03_media)
        media["id"] = f"{case.lower()}-simulated-media"
        media["images"] = copy.deepcopy(q03_media["images"][:2])
        source_images = {item["camera"]: item for item in
                         q03_media["images"][2:] + q02_media["images"][2:]}
        media["images"].extend(copy.deepcopy(source_images[camera]) for camera in cameras)
        write(f"media-manifest-{case.lower()}.json", media)
        worker = copy.deepcopy(q03_worker)
        worker.update(id=f"{case.lower()}-virtual-algorithm", seed=7000 + number,
                      fCode=fcode, version=version)
        write(f"worker-manifest-{case.lower()}.json", worker)
        digest = write(f"recipes-{case.lower()}.json", catalog)
        fixture = copy.deepcopy(q03_fixture)
        fixture.update(caseId=case, fCode=fcode)
        fixture["recipeRef"].update(recipeId=f"R008-{case}", version=version,
                                    catalogDigest=digest)
        fixture["recipeCatalogPath"] = str((root / f"recipes-{case.lower()}.json").resolve())
        fixture["imageManifestPath"] = str((root / f"media-manifest-{case.lower()}.json").resolve())
        fixture["workerManifestPath"] = str((root / f"worker-manifest-{case.lower()}.json").resolve())
        write(f"fixture-{case.lower()}.json", fixture)

    recipe = catalog["recipes"][0]
    position = next(p for p in recipe["positions"] if p["slotId"] == "P01")
    media_name = f"media-manifest-{case.lower()}.json"
    base_cases["cases"].append({
        "caseId": case,
        "recipeRef": {"recipeId": recipe["recipeId"], "version": recipe["version"],
                      "catalogDigest": digest},
        "scenarioId": "S1", "occupiedSlots": ["P01"], "fCode": fcode,
        "unitKind": "independentPart", "cameraSequence": pairs,
        "availability": "Available", "restrictions": ["TestVirtualOnly;ProductionCalibrationNotApproved"],
        "mediaSource": "Simulated/Test",
        "mediaInputs": {role: f"{media_name}#" +
                        (role if role in ("ThreeD", "F") else f"Detection/{role}")
                        for role in ["ThreeD", "F"] + sorted({camera for pair in pairs for camera in pair})},
        "objects": [{"sourceSlotId": "P01", "physicalEntityIdPattern": "{UnitId}:M01",
                     "protocolSlotIndex": 1, "sourcePoint": position["resolvedSourcePoint"],
                     "flipPosition": position["resolvedFlipPosition"],
                     "faces": position["resolvedDetectionTargetsByFace"]}],
    })

write("cases.json", base_cases)
print(json.dumps({"generated": list(matrix),
                  "cases": len(base_cases["cases"])}))

# Copied inherited manifests also resolve their immutable source images from this output root.
for manifest in root.glob("media-manifest*.json"):
    write(manifest.name, read(manifest.name))
