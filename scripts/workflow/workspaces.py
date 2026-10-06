"""Managed independent snapshots for this non-Git project. No automatic merge."""
import argparse
from contextlib import contextmanager
import getpass
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import time

import runner
from locks import file_lock, WorkspaceBusy

EXCLUDED_DIRS = {".git", "artifacts", "bin", "obj", "node_modules", "__pycache__", ".vs", ".venv"}
EXCLUDED_FILES = {"workspace.json", "auth.json", ".env"}


def inventory(root):
    """Hash only source/doc/config files; do not follow junctions or copy live DBs."""
    result = {}
    for base, dirs, files in os.walk(root, followlinks=False):
        base = Path(base)
        dirs[:] = sorted(d for d in dirs if d.lower() not in EXCLUDED_DIRS
                         and (base / d).relative_to(root).as_posix().lower() != ".specify/workflows")
        for name in [*dirs, *files]:
            path = base / name
            info = path.lstat()
            if path.is_symlink() or getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
                raise ValueError(f"拒绝复制符号链接/junction：{path}")
        for name in sorted(files):
            lower = name.lower()
            if lower in EXCLUDED_FILES or lower.startswith(".env.") or lower.endswith(
                    (".pyc", ".db", ".db-wal", ".db-shm", ".sqlite", ".sqlite-wal", ".sqlite-shm", ".mdf", ".ldf")):
                continue
            path = base / name
            result[path.relative_to(root).as_posix()] = runner.digest(path)
    return result


def targets(source, parent, name):
    if not re.fullmatch(r"[a-zA-Z0-9][a-zA-Z0-9_-]{0,47}", name) or re.fullmatch(
            r"(?i:con|prn|aux|nul|com[0-9]|lpt[0-9])", name):
        raise ValueError("工作区名须为字母数字开头及字母数字/_/-；不可使用Windows保留名称")
    source, parent = source.resolve(), parent.resolve()
    target = (parent / name).resolve()
    if parent.is_relative_to(source) or source.is_relative_to(target) or target.is_relative_to(source):
        raise ValueError("并行目录必须在源目录外，且不能是源目录祖先")
    if target.parent != parent:
        raise ValueError("工作区路径经解析后超出指定父目录")
    # The bridge, request id, phase logs and dotnet output add >180 characters
    # below the workspace root on Windows. Fail before copying rather than
    # creating a workspace that can pass Check but fail Doctor later.
    if len(str(target)) > 80:
        raise ValueError("并行工作区路径过长；请使用更短的WorkspaceParent/WorkspaceName（目标根目录不超过80字符）")
    return parent, target


def managed(target, source):
    data = runner.load(target / "workspace.json")
    if (data.get("schema") != 1 or data.get("status") != "ready"
            or Path(data["source_root"]).resolve() != source.resolve()
            or Path(data["workspace_root"]).resolve() != target.resolve()):
        raise ValueError("不是此源项目的就绪工作区；拒绝覆盖、迁移或复用半成品")
    if not (target / "dev.ps1").is_file():
        raise ValueError("工作区缺少dev.ps1")
    return data


def provision(source, parent, name, feature, source_idle, *, reuse=False):
    parent, target = targets(source, parent, name)
    if reuse:
        data = managed(target, source)
        if feature and feature != data["feature"]:
            raise ValueError("Feature与工作区预留Feature不一致")
        return target, data
    if not source_idle:
        raise ValueError("新建快照需要-SourceIdle：确认源目录在复制期间无其他会话写入")
    if not re.fullmatch(r"\d{3,}-[a-z0-9]+(?:-[a-z0-9]+)*", feature or ""):
        raise ValueError("新建并行工作区必须显式指定唯一-Feature")
    parent.mkdir(parents=True, exist_ok=True)
    # Short catalog lock covers allocation/copy only. Development is independent.
    with file_lock(parent / ".catalog.lock", workspace=parent, wait_seconds=60), \
         file_lock(source / ".specify/workflows/runner.lock", workspace=source):
        if target.exists():
            raise ValueError("工作区已存在；拒绝覆盖，需-Reuse并确认目标目录空闲")
        for sibling in parent.iterdir():
            marker = sibling / "workspace.json"
            if not marker.is_file():
                continue
            existing = runner.load(marker)
            if existing.get("feature", "").split("-")[0] == feature.split("-")[0]:
                raise ValueError("该并行目录内Feature编号已预留：" + existing["feature"])
        before = inventory(source)
        target.mkdir()
        data = dict(schema=1, status="creating", source_root=str(source.resolve()),
                    workspace_root=str(target), feature=feature, owner=getpass.getuser(),
                    created=runner.stamp(), isolation="independent-copy", files=before)
        runner.save(target / "workspace.json", data)
        try:
            for rel in before:
                destination = target / rel
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source / rel, destination)
            if inventory(source) != before or inventory(target) != before:
                raise ValueError("复制期间源文件发生变化或复制校验失败；保留快照证据，不启动开发")
            (target / ".specify/workflows").mkdir(parents=True, exist_ok=True)
            data.update(status="ready", ready=runner.stamp())
            runner.save(target / "workspace.json", data)
        except Exception as exc:
            data.update(status="failed", error=str(exc))
            runner.save(target / "workspace.json", data)
            raise
    return target, data


@contextmanager
def execution_slot(parent, wait_seconds):
    """Conservative two-job limit, shared by launchers in this workspace parent."""
    deadline = time.monotonic() + wait_seconds
    last_progress = 0
    while True:
        chosen = None
        for index in range(2):
            candidate = file_lock(parent / f".execution-{index}.lock", workspace=parent)
            try:
                candidate.__enter__()
                chosen = candidate
                break
            except WorkspaceBusy:
                continue
        if chosen:
            try:
                yield
            finally:
                chosen.__exit__(None, None, None)
            return
        if time.monotonic() >= deadline:
            raise WorkspaceBusy("parallel_capacity_busy: 两个执行席位均占用；使用-WaitSeconds 600排队")
        if time.monotonic() - last_progress >= 20:
            print("[Parallel] waiting for execution slot", file=sys.stderr, flush=True)
            last_progress = time.monotonic()
        time.sleep(0.2)


def launch(target, command):
    from execution import ProcessJob
    env = dict(os.environ, GAODE_WORKFLOW_PYTHON=sys.executable, PYTHONUTF8="1", PYTHONDONTWRITEBYTECODE="1")
    for key in ("GAODE_WORKFLOW_REQUEST", "SPECIFY_INIT_DIR", "SPECKIT_WORKFLOW_RUN_ID", "GAODE_TEST_ROOT"):
        env.pop(key, None)
    process = subprocess.Popen(command, cwd=target, env=env, stdin=subprocess.DEVNULL)
    job = None
    try:
        job = ProcessJob(process)
        return process.wait()
    finally:
        if job:
            job.close()
        if process.poll() is None:
            process.kill()
            process.wait(timeout=10)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--name", required=True)
    parser.add_argument("--parent", type=Path, default=runner.ROOT.parent / (runner.ROOT.name + "-workspaces"))
    parser.add_argument("--feature")
    parser.add_argument("--requirement", default="")
    parser.add_argument("--mode", choices=("new", "continue"), default="new")
    parser.add_argument("--milestone", choices=("M1", "M2", "All"), default="M1")
    for option in ("reuse", "source-idle", "workspace-idle"):
        parser.add_argument("--" + option, action="store_true")
    actions = parser.add_mutually_exclusive_group()
    for option in ("check", "doctor", "verify", "status", "create-only"):
        actions.add_argument("--" + option, action="store_true")
    actions.add_argument("--resume")
    parser.add_argument("--wait-seconds", type=int, default=0)
    args = parser.parse_args()
    if not 0 <= args.wait_seconds <= 600:
        parser.error("wait-seconds must be between 0 and 600")
    readonly = args.check or args.status or args.create_only or args.verify
    developing = not (readonly or args.doctor or args.verify)
    if args.reuse and not readonly and not args.workspace_idle:
        parser.error("复用并执行需-WorkspaceIdle：确认此目标工作区无其他会话写入")
    if developing and not args.resume and args.mode == "new" and not args.requirement.strip():
        parser.error("新需求不能为空")
    parent, _ = targets(runner.ROOT, args.parent, args.name)
    target, data = provision(runner.ROOT, parent, args.name, args.feature, args.source_idle, reuse=args.reuse)
    print(f"Workspace: {target}; Feature: {data['feature']}", flush=True)
    if args.create_only:
        return 0
    command = ["pwsh", "-NoProfile", "-File", str(target / ("scripts/verify.ps1" if args.verify else "dev.ps1"))]
    if args.check:
        command += ["-Check"]
    elif args.status:
        command += ["-Status"]
    elif args.doctor:
        command += ["-Doctor"]
    elif args.verify:
        command += ["-WaitSeconds", str(args.wait_seconds)]
    elif args.resume:
        command += ["-Resume", args.resume, "-WorkspaceIdle"]
    else:
        command += ["-Mode", args.mode, "-Feature", data["feature"], "-Milestone", args.milestone, "-WorkspaceIdle"]
        if args.requirement:
            command += ["-Requirement", args.requirement]
    if readonly:
        if args.verify:
            # Product integration tests can contend for machine-level timers,
            # file watchers and test-host resources even in separate copies.
            # Keep AI/design sessions parallel, but serialize full Verify.
            with file_lock(parent / ".verification.lock", workspace=parent,
                           wait_seconds=args.wait_seconds):
                return launch(target, command)
        return launch(target, command)
    with file_lock(target / ".specify/workflows/parallel-entry.lock", workspace=target,
                   wait_seconds=args.wait_seconds), execution_slot(parent, args.wait_seconds):
        return launch(target, command)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:
        print(f"{type(exc).__name__}: {exc}", file=sys.stderr)
        sys.exit(1)
