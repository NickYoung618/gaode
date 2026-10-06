"""S2 model F: two virtual groups, compressor two faces and pin one face.
Source: read-only workbook O14/P14 and O18/P18, U01/U02/U07. No E required.
Coordinates are versioned Test values in the existing SIM_MACHINE range.
"""
import copy
import hashlib
import json
import sys
from pathlib import Path

source_root = Path(__file__).resolve().parent
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("case", nargs="?", default="GROUP-F", choices=["GROUP-A", "GROUP-F"])
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
root = args.output.resolve()
if root == source_root or not root.is_dir():
    raise SystemExit("A prepared independent versioned output directory is required")
def read(name):
    return json.loads((root / name).read_text(encoding="utf-8"))
def write(name, value):
    data = (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    (root / name).write_bytes(data)
    return hashlib.sha256(data).hexdigest().upper()

group_a = args.case == "GROUP-A"
case = "GROUP-A-E" if group_a else "GROUP-F"
slug = "group-a-e" if group_a else "group-f"
members = [("BASE", 4, "K9/K18/K19"), ("CAP", 1, "L9/L18"),
           ("MOTOR", 1, "M9/M18"), ("PIN", 1, "P9/P18")] if group_a else [
           ("COMPRESSOR", 2, "O14/O18"), ("PIN", 1, "P14/P18")]
catalog = read("recipes-q09.json" if group_a else "recipes-q04.json")
recipe = catalog["recipes"][0]
recipe.update(recipeId="R008-GROUP-F", version="1.0.2-test-usr-e", model="F-Test",
              manuallySelectedScenario="S2", unitKind="looseGroup", primaryMaterial="COMPRESSOR", plcRecipeId=201)
recipe["fCode"]["virtualExactPayload"] = "TEST-TRAY-0201"
if group_a:
    recipe.update(recipeId="R008-GROUP-A-E", model="A-Test", primaryMaterial="BASE", plcRecipeId=204)
    recipe["fCode"]["virtualExactPayload"] = "TEST-TRAY-0204"
recipe["disposition"]["physicalUnit"] = "problemMembersOnly"
recipe["compositionPerOccupiedSlot"] = [
    dict(material=material, name=material, role="primary" if index == 0 else "member",
         localFaces=list(range(1, count+1)), sourceCell=cell,
         captureProfile="SIM_CAPTURE_AB", algorithmProfile="SIM_ALGORITHM")
    for index, (material, count, cell) in enumerate(members)]
for stage in recipe["execution"]["stages"]:
    face = stage["stage"]
    template = stage["targets"][0]
    stage["targets"] = [{**copy.deepcopy(template), "material": material}
                        for material, count, _ in members if face <= count]
template_position = copy.deepcopy(recipe["positions"][0])
positions = []
for index in range(1, 16):
    slot = f"P{index:02d}"
    x, y = 100 + ((index-1) % 5)*100, 100 + ((index-1)//5)*100
    position = dict(slotId=slot, expectedUnitCount=1, protocolSlotIndex=index,
                    unitIdPattern="{TrayRunId}:G:"+slot, pointRefs={}, members=[], resolvedObjects={})
    for member_index, (material, face_count, _) in enumerate(members, 1):
        member_pattern = "{UnitId}:M"+f"{member_index:02d}"
        item = copy.deepcopy(template_position)
        item.pop("members", None)
        item.pop("slotId", None)
        item.pop("unitIdPattern", None)
        item["pointRefs"] = {camera: f"{case}-{slot}-{material}-{camera}-1.0.0" for camera in "ABCD"}
        for name in ("resolvedSourcePoint", "resolvedFlipPosition"):
            point = item[name]
            point.update(id=slot, version=f"test-{slug}/1.0.0", x=x, y=y+20*(member_index-1),
                         testSourceRef=f"Test/{case}/{slot}/{material}/{name}/1.0.0")
            point["point"].update(id=slot, version=f"test-{slug}/1.0.0", x=point["x"], y=point["y"])
        item["resolvedDetectionTargetsByFace"] = {face: targets for face, targets in
            item["resolvedDetectionTargetsByFace"].items() if int(face) <= face_count}
        for face, cameras in item["resolvedDetectionTargetsByFace"].items():
            for camera, target in cameras.items():
                target.update(objectId=member_pattern, sourceSlotId=slot, protocolSlotIndex=index,
                              pointRef=item["pointRefs"][camera], coordinateConfigVersion=f"test-{slug}/1.0.0")
                target["point"].update(id=f"{slot}-{material}-face{face}-{camera}",
                                       version=f"test-{slug}/1.0.0", x=x+(10 if camera in "BD" else 0)+20*(int(face)-1),
                                       y=y+20*(member_index-1)+(10 if camera in "CD" else 0),
                                       testSourceRef=f"Test/{case}/{slot}/{material}/face{face}/{camera}/1.0.0")
                target["zBasis"]["measurementRef"].update(objectSlotId=slot, sampleId="sample-b" if slot == "P03" else "sample-a")
                target["zBasis"]["mappingSourceRef"] = target["point"]["testSourceRef"]
        position["members"].append(dict(material=material, memberIdPattern=member_pattern,
                                        handling="individualPart", pointRefs=item["pointRefs"]))
        position["resolvedObjects"][material] = item
    positions.append(position)
if group_a:
    e_catalog = read("recipes-assembly-a-e.json")
    for name in ("SIM_CAPTURE_E", "SIM_EDECODE"):
        catalog["virtualFixedProfiles"][name] = copy.deepcopy(e_catalog["virtualFixedProfiles"][name])
    recipe["eCode"] = copy.deepcopy(e_catalog["recipes"][0]["eCode"])
    recipe["eCode"].update(bindTo="GroupId", source="Workbook K9/K19; U06")
    for position in positions:
        base = position["resolvedObjects"]["BASE"]
        target = copy.deepcopy(base["resolvedDetectionTargetsByFace"]["1"]["A"])
        point_ref = f"{case}-{position['slotId']}-BASE-E-1.0.0"
        base["pointRefs"]["E"] = point_ref
        target.update(cameraId="E", pointRef=point_ref)
        target["point"].update(id=point_ref, x=target["point"]["x"]+80,
                                testSourceRef=f"Test/{case}/{position['slotId']}/BASE/E/1.0.0")
        target["zBasis"].update(offsetMm=80, minZ=85, maxZ=100)
        target["zBasis"]["mappingSourceRef"] = target["point"]["testSourceRef"]
        base["resolvedDetectionTargetsByFace"]["1"]["E"] = target
recipe["positions"] = positions
recipe["testEligibleSlots"] = ["P01", "P03"]
catalog["source"] = dict(basis="Workbook S2 model F O14/P14 O18/P18; U01/U02/U07; SIM_TRAY_5X3",
                         status="TestOnly/MultiObject", pointMapping="test-group-f/1.0.0", heightMapping="initial-measurement/1.0.0")
if group_a:
    catalog["source"].update(basis="Workbook S2 model A K9/L9/M9/P9 K18/L18/M18/P18 K19; U01/U02/U06/U07; SIM_TRAY_5X3",
                             pointMapping="test-group-a-e/1.0.0")
digest = write(f"recipes-{slug}.json", catalog)
worker = read("worker-manifest-q02.json")
worker.update(id="group-f-worker", version="1.0.0-test", fCode="TEST-TRAY-0201", seed=8201)
if group_a: worker.update(id="group-a-e-worker", fCode="TEST-TRAY-0204", eCode="TEST-S2-BASE", eCodeIdentitySuffix=True)
write(f"worker-manifest-{slug}.json", worker)
mixed_worker = copy.deepcopy(worker)
mixed_worker.update(id="group-f-mixed-worker", dispositionByTarget=[
    dict(objectIdSuffix=":G:P01:M01", localFace=1, disposition="NG"),
    dict(objectIdSuffix=":G:P01:M01", localFace=2, disposition="Pending")])
if not group_a: write("worker-manifest-group-f-mixed.json", mixed_worker)
media = read("media-manifest-q04.json")
media["id"] = "group-f-media"
if group_a:
    media["images"] += [item for item in read("media-manifest-assembly-a-e.json")["images"] if item["role"] == "E"]
write(f"media-manifest-{slug}.json", media)
fixture = read("fixture-q04.json")
fixture.update(caseId="GROUP-F", scenarioId="S2", occupiedSlots=["P01", "P03"], fCode="TEST-TRAY-0201")
if group_a: fixture.update(caseId=case, fCode="TEST-TRAY-0204")
fixture["recipeRef"].update(recipeId=recipe["recipeId"], version=recipe["version"], catalogDigest=digest)
for key, name in [("recipeCatalogPath", f"recipes-{slug}.json"), ("workerManifestPath", f"worker-manifest-{slug}.json"),
                  ("imageManifestPath", f"media-manifest-{slug}.json")]:
    fixture[key] = str((root / name).resolve())
write(f"fixture-{slug}.json", fixture)
mixed_fixture = copy.deepcopy(fixture)
mixed_fixture.update(caseId="GROUP-F-MIXED", workerManifestPath=str((root / "worker-manifest-group-f-mixed.json").resolve()))
if not group_a: write("fixture-group-f-mixed.json", mixed_fixture)
print(json.dumps(dict(caseId=case, digest=digest, groups=2, captures=28 if group_a else 12,
                      fusions=14 if group_a else 6, flips=6 if group_a else 2, eCalls=2 if group_a else 0)))
