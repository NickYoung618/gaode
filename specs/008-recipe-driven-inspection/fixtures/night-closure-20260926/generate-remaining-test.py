"""Create original T059 variants from approved Test mappings without replacing old packages."""
import copy
import hashlib
import json
from pathlib import Path

out = Path(__file__).resolve().parent
source = out.parent

def load(name):
    return json.loads((source / name).read_text(encoding="utf-8-sig"))

def save(name, value):
    path = out / name
    if path.exists():
        raise FileExistsError(path)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return path

approved = load("recipes-singleface-gates.json")["recipes"][0]["positions"][0]["resolvedSortingTargets"]
for case, base, catalog_name, worker_name, quality in [
    ("Q01-NG", "fixture.json", "recipes.json", "worker-manifest.json", "NG"),
    ("Q02-PENDING", "fixture-q02.json", "recipes-q02.json", "worker-manifest-q02.json", "Pending"),
]:
    slug = case.lower()
    fixture = load(base)
    catalog = load(catalog_name)
    recipe = catalog["recipes"][0]
    recipe["version"] = "1.1.2-test-night"
    recipe["positions"][0]["resolvedSortingTargets"] = copy.deepcopy(approved)
    catalog["source"]["basis"] += "; original T059 variant using approved same-tray P14/P15 Test destinations"
    catalog_path = save(f"recipes-{slug}.json", catalog)
    worker = load(worker_name)
    worker.update(id=f"{slug}-worker", version="1.0.0-test-night")
    if case == "Q01-NG":
        worker["detectionDisposition"] = [quality]
    else:
        # Preserve the non-contiguous P03 normal object. Only P01 requires disposition.
        # Use the actual frozen member pattern instead of guessing its prefix.
        member = recipe["positions"][0]["members"][0]["memberIdPattern"]
        unit = recipe["positions"][0]["unitIdPattern"]
        identity = member.replace("{UnitId}", unit).replace("{TrayRunId}", "RUN")
        worker["dispositionByTarget"] = [{"objectIdSuffix": identity.split(":", 1)[1],
                                          "localFace": 1, "disposition": quality}]
    worker_path = save(f"worker-manifest-{slug}.json", worker)
    fixture.update(caseId=case, recipeCatalogPath=str(catalog_path), workerManifestPath=str(worker_path))
    fixture["recipeRef"].update(version=recipe["version"], catalogDigest=hashlib.sha256(catalog_path.read_bytes()).hexdigest().upper())
    save(f"fixture-{slug}.json", fixture)

for case in ("RECOVERY-F", "Q01-PAUSE"):
    fixture = load("fixture-recovery-3d.json" if case == "RECOVERY-F" else "fixture.json")
    fixture["caseId"] = case
    save(f"fixture-{case.lower()}.json", fixture)
