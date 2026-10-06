"""Process-owned Windows file locks; metadata is diagnostic, never authority."""
from contextlib import contextmanager
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import sys
import time
import uuid


class WorkspaceBusy(ValueError):
    pass


@contextmanager
def file_lock(path, *, workspace=None, wait_seconds=0):
    import msvcrt
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    owner = path.with_name(path.name + ".json")
    token = uuid.uuid4().hex
    deadline = time.monotonic() + wait_seconds
    last_progress = 0
    with path.open("a+b") as stream:
        while True:
            stream.seek(0)
            try:
                # Windows supports locking a byte beyond EOF. Initialize only
                # after locking, so concurrent initializers cannot interfere.
                msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
                break
            except OSError as exc:
                if time.monotonic() >= deadline:
                    try:
                        detail = owner.read_text(encoding="utf-8")
                    except OSError:
                        detail = "owner metadata unavailable"
                    raise WorkspaceBusy(f"workspace_busy: 项目锁占用，未进入执行；{path}; {detail}") from exc
                if time.monotonic() - last_progress >= 20:
                    print(f"[Waiting for lock] {path}", file=sys.stderr, flush=True)
                    last_progress = time.monotonic()
                time.sleep(0.2)
        try:
            stream.seek(0, 2)
            if stream.tell() == 0:
                stream.write(b"0")
                stream.flush()
            try:
                owner.write_text(json.dumps(dict(pid=os.getpid(), token=token,
                    started=datetime.now(timezone.utc).isoformat(), workspace=str(workspace or path.parent)),
                    ensure_ascii=False), encoding="utf-8")
            except OSError:
                pass
            yield
        finally:
            # Remove diagnostics BEFORE unlocking: never delete a successor's
            # metadata. Stale files after a crash do not prevent acquisition.
            try:
                if json.loads(owner.read_text(encoding="utf-8")).get("token") == token:
                    owner.unlink()
            except (OSError, ValueError, AttributeError):
                pass
            stream.seek(0)
            msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
