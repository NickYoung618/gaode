"""Assemble the 2026-10-06 development handoff without changing live state.

Run after committing and tagging the baseline. Requires the original external
SDK/prototype and evidence directories listed below. Output must be outside Git.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import sqlite3
import subprocess
import sys
import zipfile

TAG = "migration-20261006-r1"
NAME = "Gaode-Migration-20261006"
URL = "https://github.com/NickYoung618/gaode.git"
SKIP_DIRS = {"bin", "obj", "node_modules", "dist", "__pycache__", ".venv", ".git",
             "capture-profile", "browser-profile", "edge-profile", "chromium-profile",
             "Default", "GPUPersistentCache", "cli-home", "nuget-http", "temp"}
SKIP_SUFFIXES = {".etl", ".nettrace", ".dmp", ".part", ".lock", ".pyc"}


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def run(*args, cwd=None):
    return subprocess.check_output(args, cwd=cwd, text=True, encoding="utf-8").strip()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output-parent", type=Path, default=Path("E:/dzk-delivery"))
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output_parent.resolve()
    if output.is_relative_to(source):
        raise ValueError("Output must be outside the source repository.")
    if run("git", "status", "--porcelain", cwd=source):
        raise ValueError("Commit the source before packaging.")
    commit = run("git", "rev-parse", "HEAD", cwd=source)
    if run("git", "rev-parse", TAG + "^{commit}", cwd=source) != commit:
        raise ValueError("HEAD is not the tagged baseline.")
    archive = output / (NAME + ".zip")
    if archive.exists():
        raise FileExistsError(archive)
    stage = output / ("migration-stage-" + datetime.now().strftime("%Y%m%d-%H%M%S")) / NAME
    stage.mkdir(parents=True)
    repo_relative = "E-drive/dzk/gaode-1"
    repo = stage / repo_relative
    repo.parent.mkdir(parents=True)
    print("Cloning the tagged source and complete Git history...", flush=True)
    run("git", "clone", "--no-hardlinks", str(source), str(repo))
    run("git", "remote", "set-url", "origin", URL, cwd=repo)
    run("git", "config", "core.autocrlf", "false", cwd=repo)
    run("git", "config", "core.quotepath", "false", cwd=repo)
    run("git", "config", "core.longpaths", "true", cwd=repo)
    run("git", "bundle", "create", str(stage / "gaode-repository.bundle"), "--all", cwd=source)
    write_json(repo / ".specify/feature.json", {"feature_directory": "specs/017-confirmed-plc-addresses"})
    exclusions, mappings, evidence_files = [], [], []
    evidence_path = stage / "evidence/development-evidence.zip"
    evidence_path.parent.mkdir(parents=True)
    evidence_archive = zipfile.ZipFile(evidence_path, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=3)

    def copy_file(src, dst):
        if not src.is_file():
            raise FileNotFoundError(src)
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
        if src.stat().st_size != dst.stat().st_size or digest(src) != digest(dst):
            raise RuntimeError(f"Copy changed: {src}")

    def archive_file(src, name):
        checksum = digest(src)
        method = zipfile.ZIP_STORED if src.suffix.lower() in {".zip", ".7z"} else zipfile.ZIP_DEFLATED
        evidence_archive.write(src, name, compress_type=method)
        evidence_files.append({"path": name, "source": str(src), "bytes": src.stat().st_size, "sha256": checksum})

    def archive_tree(src, prefix):
        if not src.is_dir():
            raise FileNotFoundError(src)
        print(f"Collecting {src} ...", flush=True)
        count = 0
        for base, dirs, names in os.walk(src):
            for name in list(dirs):
                path = Path(base) / name
                if name in SKIP_DIRS or path.is_symlink():
                    exclusions.append({"path": str(path), "reason": "cache/profile/build/link directory"})
                    dirs.remove(name)
            for name in names:
                path = Path(base) / name
                if path.suffix.lower() in SKIP_SUFFIXES or path.is_symlink() or name.lower() in {"auth.json", "credentials.json"}:
                    exclusions.append({"path": str(path), "reason": "trace/lock/incomplete/credential/link"})
                    continue
                archive_file(path, prefix + "/" + path.relative_to(src).as_posix())
                count += 1
        mappings.append({"source": str(src), "packagePath": "evidence/development-evidence.zip!/" + prefix, "files": count})

    for name in ("原型.zip", "软件开发SDK.zip"):
        src = source.parent / "gaode" / name
        dst = stage / "E-drive/dzk/gaode" / name
        print(f"Copying external archive {name}...", flush=True)
        copy_file(src, dst)
        mappings.append({"source": str(src), "packagePath": dst.relative_to(stage).as_posix(), "sha256": digest(dst)})

    evidence_folders = ["016-public-tray-flow", "014-special-part-rotation", "014-012-joint",
                        "recipe-authoring-012", "plc-polling-013", "017-confirmed-plc-addresses",
                        "migration-readiness-20261006", "github-migration-20261006"]
    for name in evidence_folders:
        archive_tree(source / "artifacts" / name, "repo-artifacts/" + name)
    boundary = "recipe-execution-008/009-isolation/017-confirmed-addresses-final-3-20261006"
    archive_tree(source / "artifacts" / boundary, "repo-artifacts/" + boundary)
    archive_tree(Path("E:/dzk-delivery/016-integration-20261006"), "dzk-delivery/016-integration-20261006")
    archive_tree(Path("C:/gd14v20261005/artifacts"), "C-origin/gd14v20261005/artifacts")
    for number in (16, 17, 18, 19):
        src = Path(f"C:/dzk-work/013-20261005-run{number}/attempt")
        prefix = f"C-origin/dzk-work/013-20261005-run{number}/attempt"
        # Final audit and reassessment files suffice here; underlying run evidence
        # is already collected from artifacts/plc-polling-013 above.
        for path in src.iterdir():
            if path.is_file():
                archive_file(path, prefix + "/" + path.name)
            else:
                exclusions.append({"path": str(path), "reason": "historical audit working copy/cache; top-level audit retained"})
        mappings.append({"source": str(src), "packagePath": "evidence/development-evidence.zip!/" + prefix, "scope": "top-level files"})
    evidence_archive.close()
    print("Verifying the nested historical evidence archive...", flush=True)
    with zipfile.ZipFile(evidence_path) as bundle:
        if len(bundle.namelist()) != len(evidence_files):
            raise RuntimeError("Historical evidence archive entry count differs.")
        for entry in evidence_files:
            with bundle.open(entry["path"]) as stream:
                if hashlib.file_digest(stream, "sha256").hexdigest() != entry["sha256"]:
                    raise RuntimeError("Historical evidence changed: " + entry["path"])
    write_json(stage / "evidence/EVIDENCE-MANIFEST.json", {"files": evidence_files, "hashVerification": "Passed"})
    # Keep short handoff/index files directly readable by onsite Codex. Deep run
    # evidence stays in the nested ZIP until restored to its original short root.
    for name in ("017-confirmed-plc-addresses", "migration-readiness-20261006", "github-migration-20261006"):
        for path in (source / "artifacts" / name).iterdir():
            if path.is_file() and path.suffix.lower() in {".txt", ".md", ".json"}:
                copy_file(path, repo / "artifacts" / name / path.name)

    plc_name = "Gaode-PlcProbe-0.4.0-20261006.zip"
    for name in (plc_name, plc_name + ".sha256.txt"):
        copy_file(source / "packaging/plc-field-probe" / name, stage / "tools/plc" / name)
    camera = source / "device-commissioning-0.1.1-20261003"
    for path in camera.iterdir():
        if path.is_file() and path.name not in {"archive-attempt-1-windows-paths.zip", "interrupted-full-evidence-upload.part"}:
            copy_file(path, stage / "tools/camera" / path.name)
    for name in ("中控机联调包开发提示词.txt", "迁移补充说明.txt"):
        copy_file(source / "artifacts/017-confirmed-plc-addresses" / name, stage / name)
    copy_file(source / "迁移与版本回退.txt", stage / "迁移与版本回退.txt")
    for name in ("verify_migration.py", "Verify.cmd"):
        copy_file(source / "packaging/migration" / name, stage / name)

    snapshots = []
    data = source / "artifacts/recipe-page-preview/data"
    for relative in ("recipes/recipes.db", "runtime/station01.test.db"):
        src, dst = data / relative, stage / "data-snapshots/recipe-page-preview" / relative
        dst.parent.mkdir(parents=True, exist_ok=True)
        with sqlite3.connect(src.as_uri() + "?mode=ro", uri=True) as origin, sqlite3.connect(dst) as target:
            origin.backup(target)
            result = target.execute("PRAGMA integrity_check").fetchone()[0]
            if result != "ok":
                raise RuntimeError(f"SQLite snapshot failed: {relative}")
        snapshots.append({"source": str(src), "packagePath": dst.relative_to(stage).as_posix(),
                          "integrityCheck": result, "kind": "source-machine Test snapshot; not installed as live data"})
    write_json(stage / "data-snapshots/snapshots.json", snapshots)
    write_json(stage / "evidence/relocation-map.json", mappings)
    write_json(stage / "evidence/exclusions.json", {"items": exclusions, "scope":
        "Source is complete from tagged Git. Only selected historical evidence is carried; other old archives/runs remain on source server. "
        "No build/dependency caches, Codex login/session state, active preview state or live database files are restored."})
    (stage / "START-HERE.txt").write_text(
        f"高德完整开发迁移包\n版本：{TAG}\n提交：{commit}\n仓库：{URL}\n\n"
        "先双击 Verify.cmd 校验，再阅读“迁移与版本回退.txt”。\n"
        "E-drive 是推荐恢复到 E: 的目录树，已有现场目录时先比较合并，不直接覆盖。\n"
        "开发环境安装器及 NuGet/npm 缓存未捆绑；需要联网还原或另备缓存。\n"
        "正式 PLC 业务信号与真实相机/算法接入在中控机继续完成。本包不是实机验收证明。\n",
        encoding="utf-8-sig")
    print("Hashing every delivered file...", flush=True)
    files = [{"path": path.relative_to(stage).as_posix(), "bytes": path.stat().st_size, "sha256": digest(path)}
             for path in sorted(stage.rglob("*")) if path.is_file()]
    manifest = {"schemaVersion": "gaode-migration/1", "createdUtc": datetime.now(timezone.utc).isoformat(),
                "tag": TAG, "commit": commit, "repositoryUrl": URL, "repositoryPath": repo_relative,
                "hardwareTested": False, "fileCount": len(files), "logicalBytes": sum(x["bytes"] for x in files), "files": files}
    write_json(stage / "MIGRATION-MANIFEST.json", manifest)
    subprocess.run([sys.executable, "-u", str(stage / "verify_migration.py"), str(stage)], check=True)
    print("Creating ZIP64 archive...", flush=True)
    with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=3, allowZip64=True) as bundle:
        for index, path in enumerate(sorted(stage.rglob("*")), 1):
            if path.is_file():
                method = zipfile.ZIP_STORED if path.suffix.lower() in {".zip", ".7z", ".bundle", ".pack"} else zipfile.ZIP_DEFLATED
                bundle.write(path, NAME + "/" + path.relative_to(stage).as_posix(), compress_type=method)
            if index % 2000 == 0:
                print(f"Archive progress: {index} entries...", flush=True)
    print("Verifying archived file hashes...", flush=True)
    with zipfile.ZipFile(archive) as bundle:
        expected = {NAME + "/" + item["path"]: item for item in files}
        expected[NAME + "/MIGRATION-MANIFEST.json"] = {"sha256": digest(stage / "MIGRATION-MANIFEST.json")}
        if set(bundle.namelist()) != set(expected):
            raise RuntimeError("Archive entry set differs from manifest.")
        for name, item in expected.items():
            with bundle.open(name) as stream:
                if hashlib.file_digest(stream, "sha256").hexdigest() != item["sha256"]:
                    raise RuntimeError(f"Archive hash mismatch: {name}")
    checksum = digest(archive)
    archive.with_suffix(".zip.sha256.txt").write_text(checksum + "  " + archive.name + "\n", encoding="ascii")
    result = {"archive": str(archive), "bytes": archive.stat().st_size, "sha256": checksum,
              "commit": commit, "tag": TAG, "stage": str(stage), "fileCount": len(files),
              "logicalBytes": manifest["logicalBytes"], "hashVerification": "Passed", "gitVerification": "Passed",
              "sqliteSnapshots": snapshots, "hardwareTested": False}
    write_json(output / (NAME + "-receipt.json"), result)
    print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)


if __name__ == "__main__":
    main()
