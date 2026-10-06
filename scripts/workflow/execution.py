"""Bounded Codex process execution, independent of model-produced stage reports."""
import ctypes
from ctypes import wintypes
import codecs
import os
import json
import subprocess
import time
import runner
from resilience import retry_metadata
from bounded_logs import BoundedCapture

EXECUTOR_ERRORS = ("connecting runner pipe-in", "Failed to create unified exec process", "windows sandbox failed:")
SERVICE_CAPACITY_ERRORS = (
    "selected model is at capacity",
    "model is at capacity",
    "temporarily unavailable",
    "service unavailable",
    "server is overloaded",
)
USAGE_LIMIT_ERRORS = (
    "you've hit your usage limit",
    "you have hit your usage limit",
    "usage limit",
)
RATE_LIMIT_ERRORS = (
    "rate limit",
    "too many requests",
    "http 429",
    "status code 429",
)
NETWORK_TRANSIENT_ERRORS = (
    "stream disconnected",
    "connection reset",
    "error sending request",
    "network timeout",
)
DEFAULT_MAX_LOG_BYTES = 64 * 1024 * 1024
RETRYABLE_PRETOOL_FAILURES = {
    "codex_service_capacity", "codex_rate_limit", "codex_network_transient"
}


def event_info(line):
    """Return a conservative classification for one Codex JSONL event.

    The workflow must distinguish a service rejection from a command/tool failure.
    Historical text embedded in a successful command is deliberately ignored.
    """
    try:
        event = json.loads(line)
    except ValueError:
        return None, True, None  # Unknown transport: never assume replay is safe.
    if not isinstance(event, dict):
        return None, True, None
    item = event.get("item") or {}
    if not isinstance(item, dict):
        return None, True, None
    item_type = str(item.get("type", "")).lower()
    tool_started = event.get("type") in ("item.started", "item.completed") and (
        item_type not in ("agent_message", "reasoning", "todo_list")
    )
    failure = (event.get("type") in ("error", "turn.failed") or
               (event.get("type") == "item.completed" and item_type == "command_execution"
                and (item.get("status") == "failed" or item.get("exit_code") not in (None, 0))))
    if not failure:
        return None, tool_started, None
    text = json.dumps(event, ensure_ascii=False).lower()
    # Local command runner failures must take precedence over generic timeout/rate text.
    if any(marker.lower() in text for marker in EXECUTOR_ERRORS):
        return "command_executor_failure", tool_started, text[-1000:]
    if event.get("type") not in ("error", "turn.failed"):
        return None, tool_started, None
    if any(marker in text for marker in ("authentication failed", "unauthorized", "invalid_api_key", "token expired")) or re_status(text, 401):
        return "codex_authentication", tool_started, text[-1000:]
    if any(marker in text for marker in ("permission denied", "model_not_found", "model access", "forbidden")) or re_status(text, 403):
        return "codex_model_access", tool_started, text[-1000:]
    if any(marker in text for marker in USAGE_LIMIT_ERRORS + ("usage_limit", "insufficient_quota")):
        return "codex_usage_limit", tool_started, text[-1000:]
    if event.get("type") in ("error", "turn.failed") and any(marker in text for marker in SERVICE_CAPACITY_ERRORS):
        return "codex_service_capacity", tool_started, text[-1000:]
    if any(marker in text for marker in RATE_LIMIT_ERRORS + ("rate_limit_exceeded",)) or re_status(text, 429):
        return "codex_rate_limit", tool_started, text[-1000:]
    if re_status(text, 503) or "server_is_overloaded" in text:
        return "codex_service_capacity", tool_started, text[-1000:]
    if event.get("type") in ("error", "turn.failed") and any(marker in text for marker in NETWORK_TRANSIENT_ERRORS):
        return "codex_network_transient", tool_started, text[-1000:]
    if any(marker.lower() in text for marker in EXECUTOR_ERRORS):
        return "command_executor_failure", tool_started, text[-1000:]
    if event.get("type") in ("error", "turn.failed"):
        return "codex_turn_failure", tool_started, text[-1000:]
    return None, tool_started, None


def re_status(text, code):
    import re
    return bool(re.search(r'"(?:status|status_code|http_status_code)"\s*:\s*"?' + str(code) + r'\b', text))


def event_session_id(line):
    """Read the resumable Codex session id from a JSONL event, if present."""
    try:
        event = json.loads(line)
    except ValueError:
        return None
    if not isinstance(event, dict) or event.get("type") != "thread.started":
        return None
    value = event.get("thread_id") or event.get("session_id")
    return value if isinstance(value, str) and value.strip() else None


def failed_event(line):
    """An actual failed tool event, not historical error text read from a file."""
    kind, _, _ = event_info(line)
    return kind == "command_executor_failure"


class ProcessJob:
    """Kill-on-close job for this invocation; no global process enumeration/termination."""
    def __init__(self, process):
        self.handle = None
        if os.name != "nt":
            return
        if ctypes.sizeof(ctypes.c_void_p) != 8:
            raise RuntimeError("Workflow执行包装当前要求64位Windows Python")
        self.kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        self.kernel.CreateJobObjectW.argtypes = [ctypes.c_void_p, wintypes.LPCWSTR]
        self.kernel.CreateJobObjectW.restype = ctypes.c_void_p
        self.kernel.SetInformationJobObject.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, wintypes.DWORD]
        self.kernel.AssignProcessToJobObject.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
        self.kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        self.handle = self.kernel.CreateJobObjectW(None, None)
        info = ctypes.create_string_buffer(144)  # x64 JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        ctypes.c_uint32.from_buffer(info, 16).value = 0x2000  # KILL_ON_JOB_CLOSE
        if not self.handle or not self.kernel.SetInformationJobObject(self.handle, 9, info, len(info)):
            error = ctypes.get_last_error()
            self.close()
            raise ctypes.WinError(error)
        if not self.kernel.AssignProcessToJobObject(self.handle, int(process._handle)):
            error = ctypes.get_last_error()
            self.close()
            raise ctypes.WinError(error)

    def close(self):
        if self.handle:
            self.kernel.CloseHandle(self.handle)
            self.handle = None


def execute(command, stdout, stderr, outcome, expected, timeout, *, sandbox="workspace-write", progress=None,
            max_log_bytes=None):
    for path in (stdout, stderr, outcome):
        if path.exists():
            raise FileExistsError("执行日志已存在，拒绝覆盖或自动重开：" + str(path))
    runner.save(outcome.with_suffix(".invocation.json"), dict(argv=command, cwd=str(runner.ROOT),
                timeout_seconds=timeout, sandbox=sandbox, stdin="DEVNULL"))
    result = {k: expected[k] for k in ("request_id", "phase", "nonce")}
    result.update(started=runner.stamp(), status="starting", tool_started=False,
                  incomplete_tools=[], tool_state_uncertain=False)
    started = time.monotonic()
    last_progress = started
    process, job = None, None
    capture = BoundedCapture(stdout.parent, max_log_bytes)
    positions = {stdout: 0, stderr: 0}
    decoders = {path: codecs.getincrementaldecoder("utf-8")("replace") for path in positions}
    tails = {stdout: "", stderr: ""}
    active_tools = set()
    pending_error = None
    completed = False

    def event(line):
        nonlocal pending_error, completed
        if not line.strip():
            return
        kind, tool_started, detail = event_info(line)
        result["tool_started"] |= tool_started
        session_id = event_session_id(line)
        if session_id:
            result["session_id"] = session_id
        try:
            payload = json.loads(line)
        except ValueError:
            payload = None
        if not isinstance(payload, dict):
            result["tool_state_uncertain"] = True
            return
        if tool_started:
            item = payload.get("item", {})
            identifier = item.get("id") if isinstance(item, dict) else None
            if not isinstance(identifier, str):
                result["tool_state_uncertain"] = True
            elif payload["type"] == "item.started":
                active_tools.add(identifier)
            elif item.get("status") not in ("in_progress", "running"):
                active_tools.discard(identifier)
                result["last_completed_tool"] = identifier
        if payload.get("type") == "turn.completed":
            completed, pending_error = True, None
        if kind:
            pending_error = kind, detail
            result.update(retry_metadata(line))
            # `error` can be an informational reconnect event. Let the CLI finish
            # its own retries; only a terminal event or executor fault fails fast.
            if payload.get("type") == "turn.failed" or kind == "command_executor_failure":
                result.setdefault("failure_kind", kind)
                result["failure_detail"] = detail

    def drain(final=False):
        for path in (stdout, stderr):
            if not path.is_file():
                continue
            with path.open("rb") as stream:
                stream.seek(positions[path])
                while True:
                    data = stream.read(1048576)
                    if not data:
                        break
                    positions[path] = stream.tell()
                    tails[path] += decoders[path].decode(data)
                    if path == stdout:
                        lines = tails[path].split("\n")
                        tails[path] = lines.pop()
                        for line in lines:
                            event(line)
                        if len(tails[path]) > 8388608:
                            result["failure_kind"] = "execution_event_limit"
                            result["tool_state_uncertain"] = True
                            tails[path] = ""
                    else:
                        if any(marker in tails[path] for marker in EXECUTOR_ERRORS):
                            result["failure_kind"] = "command_executor_failure"
                        tails[path] = tails[path][-65536:]
        if final and tails[stdout]:
            event(tails[stdout])
            tails[stdout] = ""

    try:
        if capture.failure_kind:
            result["failure_kind"] = capture.failure_kind
            raise RuntimeError("日志预算或磁盘余量不足，未启动Codex")
        process = subprocess.Popen(command, cwd=runner.ROOT, stdin=subprocess.DEVNULL,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                   bufsize=0, env=os.environ.copy())
        job = ProcessJob(process)
        capture.start(process, stdout, stderr)
        result.update(pid=process.pid, status="running")
        runner.save(outcome, result)
        while True:
            drain()
            if capture.failure_kind:
                result["failure_kind"] = capture.failure_kind
            if result.get("failure_kind") or process.poll() is not None:
                break
            if time.monotonic() - started >= timeout:
                result["failure_kind"] = "execution_deadline"
                break
            if progress and time.monotonic() - last_progress >= 20:
                last_progress = time.monotonic()
                progress(dict(elapsed_seconds=round(last_progress-started, 1),
                              phase=expected.get("phase"), nonce=expected.get("nonce"),
                              tool_started=result["tool_started"]))
            time.sleep(0.2)
    except BaseException as exc:
        result.update(failure_kind=result.get("failure_kind", "executor_wrapper_failure"), error=str(exc))
        raise
    finally:
        if job:
            job.close()
        if process and process.poll() is None:
            process.kill()
            process.wait(timeout=10)
        capture.join()
        drain(final=True)
        if capture.failure_kind:
            result["failure_kind"] = capture.failure_kind
            result["failure_detail"] = capture.error or "已停止本次子进程，保留限额内日志"
        if pending_error and not completed:
            result.setdefault("failure_kind", pending_error[0])
            result.setdefault("failure_detail", pending_error[1])
        if process and process.returncode != 0:
            result.setdefault("failure_kind", "codex_nonzero_exit")
        result["incomplete_tools"] = sorted(active_tools)
        result.update(status="failed" if result.get("failure_kind") else "exited",
                      elapsed_seconds=round(time.monotonic()-started, 3), finished=runner.stamp())
        result.update(log_limit_bytes=capture.max_file, request_log_bytes=capture.total)
        result["retryable"] = bool(result.get("failure_kind") in RETRYABLE_PRETOOL_FAILURES
                                   and not result.get("tool_started"))
        if process:
            result["exit_code"] = process.returncode
        runner.save(outcome, result)
    if result.get("failure_kind"):
        raise RuntimeError("Codex执行失败；拒绝发布阶段报告：" + str(outcome))
    return result
