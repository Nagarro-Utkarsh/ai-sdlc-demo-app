# AI interaction log

The hook configuration is `.github/hooks/interaction-log.json`; it is not the log file. The generated assignment log is `AI_INTERACTION_LOG.jsonl` at the repository root, beside `AGENTS.md`. Open the repository root—the folder containing `.git`, `AGENTS.md`, and `.github/hooks`—as the VS Code workspace, trust it, and select the Copilot-compatible harness (not a different provider or Cloud target). In the Chat view, use `/hooks` to confirm the workspace hook is discovered and enabled. The versioned Copilot hook configuration uses the lower-camel event names required by Copilot-compatible hooks. Start a new chat session after enabling hooks, then submit a prompt.

The `userPromptSubmitted` hook records the submitted prompt text in the JSONL record's `prompt` field with `agent_role: "user"`. Tool events, agent stop events, and custom subagent starts/results are recorded as well. Copilot's lower-camel event names are normalized to the logger's stable event names.

For example, a prompt record has this shape:

```json
{"event_type":"UserPromptSubmit","agent_role":"user","prompt":"<redacted prompt text>"}
```

The logger redacts values under sensitive field names, common inline secret assignments, bearer values, secret-bearing CLI flags, and URL credentials. Redaction is a safeguard, not a guarantee; review the log for private data before committing or sharing it. Historical summaries are not a complete prompt transcript, and missing prompt text must not be reconstructed.

## Response-capture limit

The stable main-agent `Stop` hook payload contains a transcript path and stop reason, not the assistant's final response text. The logger intentionally records that limitation instead of parsing the unstable transcript-file format or claiming complete response capture. Subagent completion payloads do expose the final response, which is recorded after redaction.

For a complete assignment hand-in, export the Copilot chat transcript using the VS Code chat export capability and review it for private data. If a prompt is available from the visible conversation but was not captured by the hook, pipe a JSON object with `event_type: "UserPromptSubmit"`, its exact `prompt`, and `capture_method: "manual from visible conversation; not a live hook"` into `python3 .github/hooks/interaction_log.py`. The logger applies the same redaction and appends it. Append required assistant response text or summaries using `/record-assistant-response` and `event_type: "ManualAssistantResponse"`. Label manually added entries as manual; never reconstruct hidden reasoning or fabricate missing turns. Hook capture only starts after the repository is opened in a VS Code Copilot harness with the hooks enabled and trusted.

## Validate the logger

Run `python3 -m unittest discover -s .github/hooks -p 'test_*.py'`. The tests cover sensitive-value redaction, Copilot user-prompt capture, manual prompt labeling, the stable Copilot tool-event shape, documented manual assistant-response records, and the explicit main-response limitation. Do not put real credentials or private customer data in prompts or the committed log.
