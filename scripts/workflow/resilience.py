"""Local service policy. Never guesses a quota reset's date or time zone."""
from datetime import datetime, timedelta, timezone
from email.utils import parsedate_to_datetime
import json
import math
import re

AUTOMATIC = {"codex_service_capacity", "codex_rate_limit", "codex_network_transient"}
RECOVERABLE = AUTOMATIC | {"codex_usage_limit"}
NON_TOOL_ITEMS = {"agent_message", "reasoning", "todo_list"}


def utc_now():
    return datetime.now(timezone.utc)


def timestamp(value):
    try:
        if isinstance(value, (int, float)) and not isinstance(value, bool):
            return datetime.fromtimestamp(value, timezone.utc)
        parsed = datetime.fromisoformat(str(value).replace("Z", "+00:00"))
        return parsed.astimezone(timezone.utc) if parsed.tzinfo else None
    except (ValueError, TypeError, OSError, OverflowError):
        return None


def retry_metadata(line, now=None):
    now = now or utc_now()
    try:
        event = json.loads(line)
    except ValueError:
        return {}
    if not isinstance(event, dict) or event.get("type") not in {"error", "turn.failed"}:
        return {}
    error = event.get("error")
    containers = [event] + ([error] if isinstance(error, dict) else [])
    candidates, hints = [], []
    for obj in containers:
        if isinstance(obj.get("message"), str):
            match = re.search(r"(?:try again at|try again in|retry after).*", obj["message"], re.I)
            if match:
                hints.append(match.group(0)[:240])
        headers = obj.get("headers", {})
        values = {str(key).lower().replace("-", "_"): value for key, value in obj.items()}
        if isinstance(headers, dict):
            values.update({str(key).lower().replace("-", "_"): value for key, value in headers.items()})
        for key in ("retry_after", "retry_after_seconds", "retry_after_ms"):
            if key not in values:
                continue
            value = values[key]
            try:
                seconds = float(value) / (1000 if key.endswith("_ms") else 1)
                if math.isfinite(seconds) and seconds >= 0:
                    candidates.append(now + timedelta(seconds=seconds))
            except (ValueError, TypeError, OverflowError):
                try:
                    date = parsedate_to_datetime(str(value))
                    if date.tzinfo:
                        candidates.append(date.astimezone(timezone.utc))
                except (ValueError, TypeError, OverflowError):
                    pass
        for key in ("resets_at", "reset_at", "retry_at"):
            date = timestamp(values.get(key))
            if date:
                candidates.append(date)
    result = {}
    if hints:
        result["retry_hint"] = hints[-1]
    if candidates:
        result["retry_not_before"] = max([now] + candidates).isoformat()
    return result


def recovery_mode(observed):
    """Resume is not exactly-once: unknown/in-flight tool effects require review."""
    if observed.get("status") not in {"failed", "paused_retryable"}:
        return None
    if observed.get("failure_kind") not in RECOVERABLE:
        return None
    if observed.get("incomplete_tools") or observed.get("tool_state_uncertain"):
        return None
    session = observed.get("session_id") or observed.get("resume_session_id")
    if observed.get("tool_started") or observed.get("mode") == "resume":
        return "resume" if session else None
    return "new" if observed.get("tool_started") is False else None


def assert_ready(observed):
    date = timestamp(observed.get("retry_not_before"))
    if date and date > utc_now():
        raise ValueError("服务要求等待至 " + date.isoformat() + "；尚未到恢复时间")
