"""Bounded pipe-to-file capture; never rotates/deletes evidence or Codex user logs."""
import os
import shutil
import threading

MIB = 1024 * 1024


def setting(name, default, minimum, maximum):
    value = int(os.environ.get(name, str(default)))
    if not minimum <= value <= maximum:
        raise ValueError(f"{name}必须在{minimum}到{maximum}之间")
    return value


class BoundedCapture:
    def __init__(self, folder, max_file_bytes=None):
        self.max_file = (max_file_bytes if max_file_bytes is not None else
                         setting("GAODE_WORKFLOW_MAX_LOG_BYTES", 64*MIB, MIB, 256*MIB))
        self.max_total = setting("GAODE_WORKFLOW_MAX_REQUEST_LOG_BYTES", 512*MIB, MIB, 2048*MIB)
        self.reserve = setting("GAODE_WORKFLOW_MIN_FREE_BYTES", 256*MIB, MIB, 4096*MIB)
        self.folder = folder
        self.total = sum(path.stat().st_size for path in folder.rglob("*.log") if path.is_file())
        self.lock = threading.Lock()
        self.threads = []
        self.failure_kind = None
        self.error = None
        if self.total >= self.max_total:
            self.failure_kind = "execution_log_limit"
        elif shutil.disk_usage(folder).free < self.reserve:
            self.failure_kind = "disk_space_low"

    def _pump(self, pipe, path):
        written = 0
        try:
            with path.open("xb", buffering=0) as output:
                while True:
                    data = pipe.read(65536)
                    if not data:
                        break
                    with self.lock:
                        if self.failure_kind:
                            continue  # Drain until the owner terminates its own child.
                        free = shutil.disk_usage(self.folder).free - self.reserve
                        allowed = max(0, min(len(data), self.max_file-written,
                                             self.max_total-self.total, free))
                        if allowed:
                            output.write(data[:allowed])
                            written += allowed
                            self.total += allowed
                        if allowed < len(data):
                            self.failure_kind = "disk_space_low" if free < len(data) else "execution_log_limit"
        except Exception as exc:
            with self.lock:
                self.failure_kind = "log_capture_failure"
                self.error = str(exc)
        finally:
            pipe.close()

    def start(self, process, stdout, stderr):
        for pipe, path in ((process.stdout, stdout), (process.stderr, stderr)):
            thread = threading.Thread(target=self._pump, args=(pipe, path), daemon=True)
            self.threads.append(thread)
            thread.start()

    def done(self):
        return all(not thread.is_alive() for thread in self.threads)

    def join(self):
        for thread in self.threads:
            thread.join(timeout=5)
        if not self.done():
            self.failure_kind = "log_capture_failure"
            self.error = "日志管道未关闭；存在未收敛的子进程"
