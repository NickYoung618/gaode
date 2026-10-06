"""Versioned virtual-only single-face fixture for shared workflow failure regressions."""
import copy
import json
import hashlib
from pathlib import Path

root = Path(__file__).resolve().parent
catalog = json.loads((root / "recipes.json").read_text(encoding="utf-8"))
sorting = json.loads((root / "recipes-q03.json").read_text(encoding="utf-8"))["recipes"][0]
recipe = catalog["recipes"][0]
recipe.update(recipeId="R008-SINGLEFACE-GATES", version="1.0.0-test")
recipe["fCode"]["virtualExactPayload"] = "TEST-TRAY-0999"
recipe["positions"][0]["resolvedSortingTargets"] = copy.deepcopy(
    sorting["positions"][0]["resolvedSortingTargets"])
catalog["source"]["basis"] = "Q01 single-face Test mapping plus confirmed Q03 same-tray Test sorting targets"
worker = json.loads((root / "worker-manifest.json").read_text(encoding="utf-8"))
worker.update(id="singleface-gates-worker", version="1.0.0-test",
              fCode=recipe["fCode"]["virtualExactPayload"])
for name, data in (("recipes-singleface-gates.json", catalog),
                   ("worker-manifest-singleface-gates.json", worker)):
    (root / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
fixture = json.loads((root / "fixture.json").read_text(encoding="utf-8"))
fixture.update(caseId="RECOVERY-3D", fCode="TEST-TRAY-0999", occupiedSlots=["P01"])
fixture["recipeRef"].update(recipeId=recipe["recipeId"], version=recipe["version"],
    catalogDigest=hashlib.sha256((root / "recipes-singleface-gates.json").read_bytes()).hexdigest().upper())
fixture.update(recipeCatalogPath=str((root / "recipes-singleface-gates.json").resolve()),
    imageManifestPath=str((root / "media-manifest.json").resolve()),
    workerManifestPath=str((root / "worker-manifest-singleface-gates.json").resolve()))
(root / "fixture-recovery-3d.json").write_text(json.dumps(fixture, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
