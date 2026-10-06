"""Own one hidden Windows Host console; never select another process by name or port."""
import ctypes
import datetime
import json
import os
from pathlib import Path
import subprocess
import sys


def main():
    if os.name != "nt":
        raise RuntimeError("This controlled Host lifecycle uses Windows console groups")
    evidence, name, host_dll, url = sys.argv[1:]
    workspace = Path(__file__).resolve().parent.parent
    root = Path(evidence).resolve(strict=True)
    allowed = workspace / "artifacts" / "011-plc-interaction-update"
    if os.environ.get("GAODE_016_TEST_ROOT"):
        declared = Path(os.environ["GAODE_016_TEST_ROOT"]).resolve(strict=True)
        allowed = (workspace / "artifacts" / "016-public-tray-flow").resolve(strict=True)
        if declared != allowed or any(os.environ.get(key) for key in ("GAODE_012_AUTHORING_EVIDENCE_ROOT", "GAODE_014_JOINT_ROOT", "GAODE_013_ATTEMPT_ROOT")):
            raise ValueError("016OwnedHostSourceMismatch")
    if os.environ.get("GAODE_012_AUTHORING_EVIDENCE_ROOT"):
        allowed = (workspace / "artifacts" / "recipe-layout-012").resolve(strict=True)
        declared = Path(os.environ["GAODE_012_AUTHORING_EVIDENCE_ROOT"]).resolve(strict=True)
        if declared != root or not root.is_relative_to(allowed) or os.environ.get("GAODE_014_JOINT_ROOT") or os.environ.get("GAODE_013_ATTEMPT_ROOT"):
            raise ValueError("012OwnedHostSourceMismatch")
    if os.environ.get("GAODE_014_JOINT_ROOT"):
        declared = Path(os.environ["GAODE_014_JOINT_ROOT"]).resolve(strict=True)
        allowed = (workspace / "artifacts" / "014-012-joint").resolve(strict=True)
        if declared != allowed or os.environ.get("GAODE_013_ATTEMPT_ROOT"):
            raise ValueError("014OwnedHostSourceMismatch")
    if os.environ.get("GAODE_013_ATTEMPT_ROOT"):
        allowed = Path(os.environ["GAODE_013_ATTEMPT_ROOT"]).resolve(strict=True)
        if Path(os.environ["GAODE_013_SOURCE_ROOT"]).resolve(strict=True) != workspace:
            raise ValueError("013OwnedHostSourceMismatch")
    if not root.is_relative_to(allowed) or name not in ("host", "host-reread"):
        raise ValueError("OwnedHostEvidenceRootRequired")
    expected = workspace / "backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll"
    if Path(host_dll).resolve(strict=True) != expected.resolve(strict=True):
        raise ValueError("OnlyThisWorkspaceHostCanBeOwned")
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = 0
    process = subprocess.Popen(
        ["dotnet", str(expected), "--urls", url], cwd=workspace,
        stdout=sys.stdout, stderr=sys.stderr, stdin=subprocess.DEVNULL,
        creationflags=subprocess.CREATE_NEW_CONSOLE | subprocess.CREATE_NEW_PROCESS_GROUP,
        startupinfo=startup,
    )
    record = dict(hostPid=process.pid, supervisorPid=os.getpid(), hidden=True,
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  executable=str(expected), url=url, state="Started")
    metadata = root / (name + "-process.json")

    def save():
        metadata.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")

    save()
    command = sys.stdin.readline().strip()
    if command not in ("stop", ""):
        record.update(state="InvalidControl", normalShutdown=False)
        save()
        # Keep ownership until the parent performs its own bounded failure cleanup.
        process.wait()
        return 2
    if process.poll() is not None:
        record.update(state="ExitedBeforeStop", exitCode=process.returncode, normalShutdown=False)
        save()
        return 2
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.FreeConsole()
    if not kernel.AttachConsole(process.pid):
        record.update(state="AttachFailed", error=ctypes.get_last_error(), normalShutdown=False)
        save()
        process.wait()
        return 2
    try:
        # CTRL_BREAK targets only the process group created above, never console group 0.
        # The attached supervisor must survive the console control delivery and verify the child exit.
        control_handler = ctypes.WINFUNCTYPE(ctypes.c_int, ctypes.c_uint)(lambda event: int(event in (0, 1)))
        if not kernel.SetConsoleCtrlHandler(control_handler, True):
            raise OSError(ctypes.get_last_error(), "SupervisorConsoleHandlerFailed")
        signalled = bool(kernel.GenerateConsoleCtrlEvent(1, process.pid))
        record.update(signal="CTRL_BREAK", groupId=process.pid, signalAccepted=signalled,
                      control=command or "parent-eof")
        save()
    finally:
        kernel.FreeConsole()
    try:
        code = process.wait(timeout=40)
    except subprocess.TimeoutExpired:
        record.update(state="ShutdownTimeout", normalShutdown=False)
        save()
        process.wait()  # Parent owns bounded kill-tree cleanup, never a normal-shutdown pass.
        return 2
    record.update(state="Exited", exitCode=code, normalShutdown=signalled and code == 0,
                  exitedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    save()
    return 0 if record["normalShutdown"] else 2


if __name__ == "__main__":
    sys.exit(main())
