#!/usr/bin/env python3
"""Append redacted Copilot hook events to a repository-root JSONL audit log."""

from __future__ import annotations

import json
import os
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

if os.name == "nt":
    import msvcrt
else:
    import fcntl


SECRET_KEY = re.compile(
    r"password|passwd|token|secret|authorization|api[_-]?key|credential|private[_-]?key",
    re.IGNORECASE,
)
INLINE_SECRET = re.compile(
    r"(?i)(\b(?:password|passwd|token|secret|authorization|api[_-]?key|credential|private[_-]?key)\b"
    r"['\"]?\s*(?:is\s+|[:=]\s*))(\"[^\"]*\"|'[^']*'|[^\s,;}\]]+)"
)
SECRET_ARGUMENT = re.compile(
    r"(?i)(--(?:password|token|secret|api-key|authorization)\s+)([^\s]+)"
)
BEARER = re.compile(r"(?i)(\bBearer\s+)[A-Za-z0-9._~+/-]+=*")
URL_CREDENTIALS = re.compile(r"(?i)(https?://)[^/\s:@]+:[^/\s@]+@")
KNOWN_EVENTS = {
    "SessionStart",
    "SessionEnd",
    "UserPromptSubmit",
    "PreToolUse",
    "PostToolUse",
    "PostToolUseFailure",
    "Stop",
    "SubagentStart",
    "SubagentStop",
    "ErrorOccurred",
    "ManualAssistantResponse",
}
COPILOT_EVENT_NAMES = {
    "sessionStart": "SessionStart",
    "sessionEnd": "SessionEnd",
    "userPromptSubmitted": "UserPromptSubmit",
    "preToolUse": "PreToolUse",
    "postToolUse": "PostToolUse",
    "postToolUseFailure": "PostToolUseFailure",
    "agentStop": "Stop",
    "subagentStart": "SubagentStart",
    "subagentStop": "SubagentStop",
    "errorOccurred": "ErrorOccurred",
}


def redact(value: Any, key: str = "") -> Any:
    if SECRET_KEY.search(key):
        return "[REDACTED]"
    if isinstance(value, dict):
        return {str(item_key): redact(item, str(item_key)) for item_key, item in value.items()}
    if isinstance(value, list):
        return [redact(item) for item in value]
    if isinstance(value, str):
        value = INLINE_SECRET.sub(r'\1"[REDACTED]"', value)
        value = SECRET_ARGUMENT.sub(r"\1[REDACTED]", value)
        value = BEARER.sub(r"\1[REDACTED]", value)
        return URL_CREDENTIALS.sub(r"\1[REDACTED]@", value)
    return value


def _event_field(event: dict[str, Any], vscode_name: str, copilot_name: str | None = None) -> Any:
    return event.get(vscode_name, event.get(copilot_name)) if copilot_name else event.get(vscode_name)


def _timestamp(event: dict[str, Any]) -> str:
    value = event.get("timestamp")
    if isinstance(value, (int, float)):
        return datetime.fromtimestamp(value / 1000, tz=timezone.utc).isoformat()
    if isinstance(value, str) and value:
        return value
    return datetime.now(timezone.utc).isoformat()


def _canonical_event_name(name: Any) -> Any:
    return COPILOT_EVENT_NAMES.get(name, name) if isinstance(name, str) else name


def record_for(event: dict[str, Any]) -> dict[str, Any]:
    raw_name = event.get("hook_event_name") or event.get("eventName") or event.get("event_type") or "unknown"
    name = _canonical_event_name(raw_name)
    record: dict[str, Any] = {
        "timestamp": _timestamp(event),
        "event_type": name,
        "provider": "GitHub Copilot",
        "session_id": _event_field(event, "session_id", "sessionId"),
        "agent_role": "assistant",
        "capture_method": f"VS Code Copilot {name} hook",
    }

    if name == "UserPromptSubmit":
        record.update(
            agent_role="user",
            prompt=redact(event.get("prompt", "")),
            capture_method=event.get("capture_method", record["capture_method"]),
        )
    elif name == "PostToolUse":
        record.update(
            tool_name=_event_field(event, "tool_name", "toolName"),
            tool_input=redact(_event_field(event, "tool_input", "toolArgs")),
            tool_result=redact(_event_field(event, "tool_result", "toolResult")),
        )
    elif name == "PostToolUseFailure":
        record.update(
            tool_name=_event_field(event, "tool_name", "toolName"),
            tool_input=redact(_event_field(event, "tool_input", "toolArgs")),
            error=redact(event.get("error", "")),
        )
    elif name == "Stop":
        record.update(
            stop_reason=_event_field(event, "stop_reason", "stopReason"),
            transcript_available=bool(_event_field(event, "transcript_path", "transcriptPath")),
            response_capture=(
                "The stable Stop hook provides a transcript path, not assistant response text. "
                "Export the Copilot chat and append a redacted manual record when full responses are required."
            ),
        )
    elif name in {"SessionStart", "SessionEnd"}:
        record["reason"] = event.get("reason")
    elif name == "ErrorOccurred":
        record["error"] = redact(event.get("error", event.get("message", "")))
    elif name == "SubagentStart":
        record.update(
            agent_role="subagent",
            agent_type=_event_field(event, "agent_type", "agentType"),
            agent_name=_event_field(event, "agent_name", "agentName"),
        )
    elif name == "SubagentStop":
        record.update(
            agent_role=_event_field(event, "agent_type", "agentType") or "subagent",
            agent_name=_event_field(event, "agent_name", "agentName"),
            response=redact(_event_field(event, "last_assistant_message", "response")),
        )
    elif name == "ManualAssistantResponse":
        record.update(
            agent_role="assistant",
            response=redact(event.get("response", "")),
            capture_method=event.get("capture_method", "manual Copilot chat record"),
        )
    else:
        record["capture_method"] = "Unknown Copilot hook event; payload fields omitted"

    return redact(record)


def find_repo_root(start: Path) -> Path:
    for candidate in (start, *start.parents):
        if (candidate / ".git").exists():
            return candidate
    raise RuntimeError(f"Could not locate the Git repository root from {start}")


def append_record(log_path: Path, record: dict[str, Any]) -> None:
    line = json.dumps(record, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n"
    lock_path = log_path.with_suffix(log_path.suffix + ".lock")
    log_path.parent.mkdir(parents=True, exist_ok=True)
    with lock_path.open("a+b") as lock_file:
        if os.name == "nt":
            lock_file.seek(0, os.SEEK_END)
            if lock_file.tell() == 0:
                lock_file.write(b"\0")
                lock_file.flush()
            lock_file.seek(0)
            msvcrt.locking(lock_file.fileno(), msvcrt.LK_LOCK, 1)
        else:
            fcntl.flock(lock_file.fileno(), fcntl.LOCK_EX)
        try:
            with log_path.open("a", encoding="utf-8") as log_file:
                log_file.write(line)
                log_file.flush()
                os.fsync(log_file.fileno())
        finally:
            if os.name == "nt":
                lock_file.seek(0)
                msvcrt.locking(lock_file.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(lock_file.fileno(), fcntl.LOCK_UN)


def main() -> None:
    event = json.load(sys.stdin)
    if not isinstance(event, dict):
        raise ValueError("Copilot hook input must be a JSON object")
    name = event.get("hook_event_name") or event.get("eventName") or event.get("event_type")
    if not isinstance(name, str) or _canonical_event_name(name) not in KNOWN_EVENTS:
        raise ValueError(f"Unsupported Copilot hook event: {name!r}")
    cwd = event.get("cwd") or Path.cwd()
    repo_root = find_repo_root(Path(cwd).resolve())
    append_record(repo_root / "AI_INTERACTION_LOG.jsonl", record_for(event))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"Copilot interaction log hook failed: {error}", file=sys.stderr)
        raise
