#!/usr/bin/env python3
"""Maintain redacted Copilot prompt and response records in the repository-root JSONL log."""

from __future__ import annotations

import json
import os
import re
import sys
import tempfile
from contextlib import contextmanager
from pathlib import Path
from typing import Any, Iterator

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
PROMPT_EVENT_NAMES = {"UserPromptSubmit", "userPromptSubmitted"}
ASSISTANT_EVENT_NAMES = {"Stop", "agentStop"}
RESPONSE_KEYS = (
    "response",
    "assistantResponse",
    "assistant_response",
    "last_assistant_message",
    "answer",
    "output",
    "content",
)
TRANSCRIPT_LIMIT_BYTES = 50 * 1024 * 1024


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


def record_for(event: dict[str, Any], default_event_name: str | None = None) -> dict[str, Any]:
    event_name = (
        event.get("hook_event_name")
        or event.get("eventName")
        or event.get("event_type")
        or default_event_name
    )
    if event_name not in PROMPT_EVENT_NAMES:
        raise ValueError(f"Unsupported Copilot hook event: {event_name!r}")

    prompt = event.get("prompt")
    if not isinstance(prompt, str):
        raise ValueError("Copilot user prompt event must include a string prompt")

    response_value = ""
    for key in RESPONSE_KEYS:
        if key in event and event[key] is not None:
            response_value = event[key]
            break
    if not isinstance(response_value, str):
        response_value = json.dumps(response_value, ensure_ascii=False) if response_value is not None else ""

    return {"prompt": redact(prompt), "response": redact(response_value)}


def _text_from_content(value: Any) -> str:
    if isinstance(value, str):
        return value
    if isinstance(value, list):
        return "".join(_text_from_content(item) for item in value)
    if isinstance(value, dict):
        if isinstance(value.get("text"), str):
            return value["text"]
        for key in ("content", "message", "parts", "value", "data"):
            if key in value:
                return _text_from_content(value[key])
    return ""


def _transcript_messages(value: Any) -> Iterator[tuple[str, str]]:
    if isinstance(value, list):
        for item in value:
            yield from _transcript_messages(item)
        return
    if not isinstance(value, dict):
        return

    message_type = str(
        value.get("role")
        or value.get("type")
        or value.get("event_type")
        or value.get("eventType")
        or ""
    ).lower()
    message_keys = {
        "user": (
            "user_content",
            "userContent",
            "prompt",
            "user_prompt",
            "userPrompt",
            "content",
            "message",
            "data",
        ),
        "assistant": (
            "assistant_content",
            "assistantContent",
            "last_assistant_message",
            "response",
            "assistant_response",
            "assistantResponse",
            "content",
            "message",
            "data",
        ),
    }

    for role in ("user", "assistant"):
        if message_type == role or message_type.endswith(f".{role}.message") or message_type == f"{role}.message":
            for key in message_keys[role]:
                if key in value:
                    text = _text_from_content(value[key])
                    if text:
                        yield role, text
                        return

    if "user_content" in value or "userContent" in value:
        text = _text_from_content(value.get("user_content", value.get("userContent")))
        if text:
            yield "user", text
    if "assistant_content" in value or "assistantContent" in value:
        text = _text_from_content(value.get("assistant_content", value.get("assistantContent")))
        if text:
            yield "assistant", text

    for key, item in value.items():
        if key not in {"user_content", "userContent", "assistant_content", "assistantContent"}:
            yield from _transcript_messages(item)


def transcript_exchange(transcript_path: str | Path) -> tuple[str, str]:
    path = Path(transcript_path)
    if not path.is_file():
        raise FileNotFoundError(f"Copilot transcript does not exist: {path}")
    if path.stat().st_size > TRANSCRIPT_LIMIT_BYTES:
        raise ValueError(f"Copilot transcript exceeds the {TRANSCRIPT_LIMIT_BYTES}-byte limit")

    content = path.read_text(encoding="utf-8")
    try:
        transcript = json.loads(content)
        records = [transcript]
    except json.JSONDecodeError:
        records = [json.loads(line) for line in content.splitlines() if line.strip()]

    prompt = ""
    response = ""
    for role, text in _transcript_messages(records):
        if role == "user":
            prompt = text
            response = ""
        elif prompt:
            response = text

    if not prompt:
        raise ValueError(f"Could not find the latest user prompt in Copilot transcript: {path}")
    if not response:
        raise ValueError(f"Could not find the final assistant response in Copilot transcript: {path}")
    return prompt, response


@contextmanager
def _locked_log(log_path: Path) -> Iterator[None]:
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
            yield
        finally:
            if os.name == "nt":
                lock_file.seek(0)
                msvcrt.locking(lock_file.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(lock_file.fileno(), fcntl.LOCK_UN)


def find_repo_root(start: Path) -> Path:
    for candidate in (start, *start.parents):
        if (candidate / ".git").exists():
            return candidate
    raise RuntimeError(f"Could not locate the Git repository root from {start}")


def append_record(log_path: Path, record: dict[str, Any]) -> None:
    line = json.dumps(record, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n"
    with _locked_log(log_path):
        with log_path.open("a", encoding="utf-8") as log_file:
            log_file.write(line)
            log_file.flush()
            os.fsync(log_file.fileno())


def update_response(log_path: Path, prompt: str, response: str) -> None:
    redacted_prompt = redact(prompt)
    redacted_response = redact(response)
    with _locked_log(log_path):
        records = [json.loads(line) for line in log_path.read_text(encoding="utf-8").splitlines()]
        updated = False
        for index in range(len(records) - 1, -1, -1):
            if records[index].get("prompt") == redacted_prompt and not records[index].get("response"):
                records[index]["response"] = redacted_response
                updated = True
                break
        if not updated:
            for index in range(len(records) - 1, -1, -1):
                if not records[index].get("response"):
                    records[index]["response"] = redacted_response
                    updated = True
                    break
        if not updated:
            raise ValueError("No pending interaction-log entry matches the completed prompt")

        temporary_path = None
        try:
            with tempfile.NamedTemporaryFile(
                mode="w",
                encoding="utf-8",
                dir=log_path.parent,
                prefix=f".{log_path.name}.",
                suffix=".tmp",
                delete=False,
            ) as temporary_file:
                temporary_path = Path(temporary_file.name)
                for record in records:
                    temporary_file.write(
                        json.dumps(record, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n"
                    )
                temporary_file.flush()
                os.fsync(temporary_file.fileno())
            os.replace(temporary_path, log_path)
        finally:
            if temporary_path is not None:
                temporary_path.unlink(missing_ok=True)


def main() -> None:
    event = json.load(sys.stdin)
    if not isinstance(event, dict):
        raise ValueError("Copilot hook input must be a JSON object")
    configured_event_name = sys.argv[1] if len(sys.argv) > 1 else None
    cwd = event.get("cwd") or Path.cwd()
    repo_root = find_repo_root(Path(cwd).resolve())
    event_name = (
        event.get("hook_event_name")
        or event.get("eventName")
        or event.get("event_type")
        or configured_event_name
    )
    log_path = repo_root / "AI_INTERACTION_LOG.jsonl"
    if event_name in PROMPT_EVENT_NAMES:
        append_record(log_path, record_for(event, configured_event_name))
    elif event_name in ASSISTANT_EVENT_NAMES:
        response_value = next(
            (
                event[key]
                for key in RESPONSE_KEYS
                if key != "content" and event.get(key) is not None
            ),
            None,
        )
        response = _text_from_content(response_value)
        if not response and response_value is not None:
            response = json.dumps(response_value, ensure_ascii=False)
        prompt = ""
        if not response:
            transcript_path = (
                event.get("agent_transcript_path")
                or event.get("transcript_path")
                or event.get("transcriptPath")
            )
            if not isinstance(transcript_path, str) or not transcript_path:
                raise ValueError(
                    "Copilot agent-stop event must include assistant response text or a transcript path"
                )
            prompt, response = transcript_exchange(transcript_path)
        update_response(log_path, prompt, response)
    else:
        raise ValueError(f"Unsupported Copilot hook event: {event_name!r}")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"Copilot interaction log hook failed: {error}", file=sys.stderr)
        raise
