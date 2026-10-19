---
name: record-assistant-response
description: "Record a redacted Copilot response summary that cannot be captured by the stable Stop hook."
argument-hint: "Paste or summarize the Copilot response and its user decision"
agent: ask
---

The repository's stable Copilot `Stop` hook does not expose the assistant response body. For this interaction, prepare one JSON object with `event_type` set to `ManualAssistantResponse`, `provider` set to `GitHub Copilot`, `agent_role` set to `assistant`, and `response` containing the supplied visible response or an explicitly labeled summary. Include `capture_method: "manual Copilot chat record"` and redact secrets. Do not invent earlier conversation content.

Append the object by piping its single-line JSON representation to `python3 .github/hooks/interaction_log.py`. The logger accepts `event_type` for this manual event, applies the same redaction, and appends it to the repository-root JSONL log.
