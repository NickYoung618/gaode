"""Affected script architecture scan; no 009/010 historical runtime profile or acceptance claim."""
from pathlib import Path
import importlib.util
import json
import sys

root = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("recipe_boundary_010", root / "scripts/workflow/recipe_execution_010.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
report = module.check_repository_scripts()
output = root / "artifacts/recipe-ui-fix-012" / (sys.argv[1] if len(sys.argv) > 1 else "script-boundaries.json")
if output.exists():
    raise RuntimeError("Preserve prior evidence; choose a new report name")
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"scope": "AffectedStaticScriptBoundary", "files": len(report["files"]), "cases": len(report["cases"]),
                  "errors": report.get("errors", []), "result": report.get("result")}, ensure_ascii=False))
if not report["files"] or not report["cases"] or report.get("errors") or report.get("result") != "Passed":
    raise SystemExit(1)
