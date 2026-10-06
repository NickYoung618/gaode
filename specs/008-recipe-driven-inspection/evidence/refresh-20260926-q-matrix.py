"""Refresh the dated current-run table while retaining the historical matrix."""

import json
import re
from pathlib import Path

feature = Path(__file__).resolve().parents[1]
repo = feature.parents[1]
artifacts = repo / "artifacts" / "recipe-execution-008"
cases = json.loads((feature / "fixtures" / "cases.json").read_text(encoding="utf-8"))
begin = "<!-- 20260926-Q-BATCH-BEGIN -->"
end = "<!-- 20260926-Q-BATCH-END -->"
lines = [begin, "## 2026-09-26 当前批次逐Q运行矩阵", "",
         "本表仅将正式 WPF 页面同一 run 到 Final 记作 Passed。后端 API 的 Final 仅列作子能力证据；原下方矩阵为历史设计/运行快照。", "",
         "| Q | 配方版本 | 完整相机序列 | Host / PLC 构建 SHA-256 | 页面 runId / Final | 证据 | 状态 |",
         "| --- | --- | --- | --- | --- | --- | --- |"]
batch_roots = [artifacts / "page-batch-20260926",
               artifacts / "page-batch-20260926-v2"]
for item in cases["cases"]:
    case = item["caseId"]
    if not (case.startswith("Q") and len(case) == 3 and case[1:].isdigit() and 1 <= int(case[1:]) <= 22):
        continue
    sequence = "→".join(item.get("cameraSequence", [item["cameraPair"]] if "cameraPair" in item else []))
    version = item["recipeRef"]["version"]
    page_root = None
    if case == "Q03":
        page_root = artifacts / "page-q03-interactive-20260926-v3" / "Q03"
    for batch in batch_roots:
        for result in sorted(batch.glob(f"runs/job-*-{case}/validation-result.json")):
            if not re.fullmatch(rf"job-[0-9]{{3}}-{case}", result.parent.name):
                continue
            if json.loads(result.read_text(encoding="utf-8-sig"))["exitCode"] == 0:
                candidate = result.parent / case
                page_file = candidate / "recipe-webview2-page-evidence.json"
                if page_file.exists() and json.loads(page_file.read_text(encoding="utf-8-sig"))["outcome"] == "FinalPageDisplayed":
                    page_root = candidate
    build = "—"
    page = "—"
    evidence = "—"
    state = "NotRun"
    if page_root:
        process = json.loads((page_root / "process.json").read_text(encoding="utf-8-sig"))
        proof = json.loads((page_root / "recipe-webview2-page-evidence.json").read_text(encoding="utf-8-sig"))
        build = process["configuration"]["hostDllSha256"][:12] + " / " + process["configuration"]["plcDllSha256"][:12]
        page = str(proof["receipt"]["body"]["runId"]) + " / Final"
        evidence = f"[正式页面](../../{page_root.relative_to(repo).as_posix()}/recipe-webview2-page-evidence.json)"
        if (page_root / "route-validation.json").exists():
            evidence += f"、[逐步核验](../../{page_root.relative_to(repo).as_posix()}/route-validation.json)"
        state = "Passed"
    else:
        backend = artifacts / f"backend-{case.lower()}-20260926" / "result.json"
        if backend.exists():
            fact = json.loads(backend.read_text(encoding="utf-8-sig"))
            process = json.loads((backend.parent / "process.json").read_text(encoding="utf-8-sig"))
            build = process["configuration"]["hostDllSha256"][:12] + " / " + process["configuration"]["plcDllSha256"][:12]
            page = f"后端 {fact['runId']} / 页面未运行"
            evidence = f"[后端子能力](../../{backend.relative_to(repo).as_posix()})"
            if (backend.parent / "route-validation.json").exists():
                evidence += f"、[逐步核验](../../{backend.parent.relative_to(repo).as_posix()}/route-validation.json)"
    lines.append(f"| {case} | R008-{case}/{version} | {sequence} | {build} | {page} | {evidence} | {state} |")
lines += ["", "另有 Q04 Flip_OK 受控超时失败包 `artifacts/recipe-execution-008/backend-q04-flip-ack-hold-20260926/`：首面后未采第二面、未到 Final。", end, ""]
matrix = feature / "coverage-matrix.md"
old = matrix.read_text(encoding="utf-8")
block = "\n".join(lines)
if begin in old:
    start = old.index(begin)
    stop = old.index(end, start) + len(end)
    updated = old[:start] + block.rstrip() + old[stop:]
else:
    updated = block + "\n\n" + old
matrix.write_text(updated, encoding="utf-8")
