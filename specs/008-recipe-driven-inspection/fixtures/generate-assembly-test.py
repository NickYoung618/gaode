"""S3 model A: BASE two faces, PIN one; E on BASE per Q9/Q18/Q19 and T9/T18.
Virtual-only derived coordinates reuse the versioned multi-object Test range.
"""
import copy
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parent
def read(name):
    return json.loads((root / name).read_text(encoding="utf-8"))
def write(name, value):
    data = (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    (root / name).write_bytes(data)
    return hashlib.sha256(data).hexdigest().upper()

catalog = read("recipes-group-f.json")
recipe = catalog["recipes"][0]
recipe.update(recipeId="R008-ASSEMBLY-A-E", version="1.0.1-test", model="A-Test", manuallySelectedScenario="S3",
              unitKind="assembledEntity", primaryMaterial="BASE", plcRecipeId=207)
recipe["fCode"]["virtualExactPayload"] = "TEST-TRAY-0207"
recipe["execution"]["route"] = "ordinaryAssembly"
recipe["disposition"]["physicalUnit"] = "wholeAssembly"
recipe["testEligibleSlots"] = ["P01"]
recipe["eCode"].update(enabled=True, reader="E", representativeMaterial="BASE", bindTo="AssemblyId",
                       requiredForOk=False, readAt="firstAccessibleFace", source="Workbook Q9/Q19; U06",
                       captureProfile="SIM_CAPTURE_E", algorithmProfile="SIM_EDECODE")
for material in recipe["compositionPerOccupiedSlot"]:
    if material["material"] == "COMPRESSOR":
        material.update(material="BASE", name="BASE", sourceCell="Q9/Q18/Q19")
    else:
        material["sourceCell"] = "T9/T18"
for stage in recipe["execution"]["stages"]:
    for target in stage["targets"]:
        if target["material"] == "COMPRESSOR":
            target["material"] = "BASE"
for position in recipe["positions"]:
    position["unitIdPattern"] = position["unitIdPattern"].replace(":G:", ":A:")
    base = position["resolvedObjects"].pop("COMPRESSOR")
    position["resolvedObjects"]["BASE"] = base
    for member in position["members"]:
        member["handling"] = "partOfWholeAssembly"
        if member["material"] == "COMPRESSOR":
            member["material"] = "BASE"
            base["pointRefs"]["E"] = f"ASSEMBLY-A-{position['slotId']}-BASE-E-1.0.0"
            member["pointRefs"] = base["pointRefs"]
    position["resolvedSourcePoint"] = copy.deepcopy(base["resolvedSourcePoint"])
    position["resolvedFlipPosition"] = copy.deepcopy(base["resolvedFlipPosition"])
    position["resolvedSortingTargets"] = copy.deepcopy(base["resolvedSortingTargets"])
    e = copy.deepcopy(base["resolvedDetectionTargetsByFace"]["1"]["A"])
    e.update(cameraId="E", pointRef=base["pointRefs"]["E"])
    e["point"].update(id=f"ASSEMBLY-A-{position['slotId']}-BASE-E", version="test-assembly-e/1.0.0",
                      testSourceRef=f"Test/Assembly/A/{position['slotId']}/BASE/EScan/1.0.0")
    e["zBasis"].update(offsetMm=80.0, minZ=85.0, maxZ=100.0, mappingSourceRef=e["point"]["testSourceRef"])
    e["coordinateConfigVersion"] = "test-assembly-e/1.0.0"
    base["resolvedDetectionTargetsByFace"]["1"]["E"] = e
catalog["virtualFixedProfiles"]["SIM_CAPTURE_E"] = copy.deepcopy(catalog["virtualFixedProfiles"]["SIM_CAPTURE_AB"])
catalog["virtualFixedProfiles"]["SIM_CAPTURE_E"]["lightChannel"] = "sim-light-e"
catalog["virtualFixedProfiles"]["SIM_EDECODE"] = copy.deepcopy(catalog["virtualFixedProfiles"]["SIM_ALGORITHM"])
catalog["source"] = dict(basis="Workbook S3 model A Q9/Q18/Q19 T9/T18; U06; Test GROUP-F coordinate range",
                         status="TestOnly/AssemblyWithE", pointMapping="test-assembly-e/1.0.0", heightMapping="initial-measurement/1.0.0")
digest = write("recipes-assembly-a-e.json", catalog)
worker = read("worker-manifest-group-f.json")
worker.update(id="assembly-a-e-worker", fCode="TEST-TRAY-0207", seed=8202,
              eCode="TEST-BASE-A", eCodeIdentitySuffix=True)
write("worker-manifest-assembly-a-e.json", worker)
error_worker = copy.deepcopy(worker)
error_worker.update(id="assembly-a-e-error-worker", eDecodeError="ControlledEDecodeFailure")
write("worker-manifest-assembly-a-e-error.json", error_worker)
ng_worker = copy.deepcopy(worker)
ng_worker.update(id="assembly-a-e-ng-worker", dispositionByTarget=[
    dict(objectIdSuffix=":A:P01:M01", localFace=1, disposition="NG"),
    dict(objectIdSuffix=":A:P01:M01", localFace=2, disposition="Pending")])
write("worker-manifest-assembly-a-e-ng.json", ng_worker)
worker["eCode"] = None
worker["id"] = "assembly-a-e-no-code-worker"
write("worker-manifest-assembly-a-e-no-code.json", worker)
media = read("media-manifest-group-f.json")
media["id"] = "assembly-a-e-media"
e_image = copy.deepcopy(next(i for i in media["images"] if i["role"] == "F"))
e_image.update(role="E", camera="E")
media["images"].append(e_image)
write("media-manifest-assembly-a-e.json", media)
fixture = read("fixture-group-f.json")
fixture.update(caseId="ASSEMBLY-A-E", scenarioId="S3", occupiedSlots=["P01"], fCode="TEST-TRAY-0207")
fixture["recipeRef"].update(recipeId=recipe["recipeId"], version=recipe["version"], catalogDigest=digest)
for key, name in [("recipeCatalogPath", "recipes-assembly-a-e.json"), ("workerManifestPath", "worker-manifest-assembly-a-e.json"),
                  ("imageManifestPath", "media-manifest-assembly-a-e.json")]:
    fixture[key] = str((root / name).resolve())
write("fixture-assembly-a-e.json", fixture)
fixture["caseId"] = "ASSEMBLY-A-E-NOCODE"
fixture["workerManifestPath"] = str((root / "worker-manifest-assembly-a-e-no-code.json").resolve())
write("fixture-assembly-a-e-no-code.json", fixture)
for variant in ("error", "ng"):
    fixture["caseId"] = f"ASSEMBLY-A-E-{variant.upper()}"
    fixture["workerManifestPath"] = str((root / f"worker-manifest-assembly-a-e-{variant}.json").resolve())
    write(f"fixture-assembly-a-e-{variant}.json", fixture)
print(json.dumps(dict(caseId="ASSEMBLY-A-E", digest=digest, captures=6, fusions=3, flips=1, eCalls=1)))
