# AI interaction log

The hook configuration is `.github/hooks/interaction-log.json`; it is not the log file. The generated assignment log is `AI_INTERACTION_LOG.jsonl` at the repository root, beside `AGENTS.md`. Open the repository root—the folder containing `.git`, `AGENTS.md`, and `.github/hooks`—as the VS Code workspace, trust it, and select a Copilot-compatible harness. In the Chat view, use `/hooks` to confirm the workspace hook is discovered and enabled. Start a new chat session after enabling hooks, then submit a prompt.

The `userPromptSubmitted` hook writes one JSON object per prompt with exactly two fields:

```json
{"prompt":"<redacted prompt text>","response":"<redacted assistant response text or empty string>"}
```

The `userPromptSubmitted` event does not include the assistant response, so the response is initially empty. The configured `agentStop` hook first uses `last_assistant_message` when present; otherwise it reads the exchange from `agent_transcript_path` (also accepting `transcript_path`/`transcriptPath`). Transcript paths are optional and transcript formats are not a stable hook API, so this fallback is best-effort. When the transcript's prompt differs from the submitted prompt, the logger fills the newest pending response rather than leaving it blank. If neither assistant text nor a usable transcript is available, it reports an explicit hook error rather than silently claiming success.

The logger redacts values under sensitive field names, common inline secret assignments, bearer values, secret-bearing CLI flags, and URL credentials. Redaction is a safeguard, not a guarantee; review the log for private data before committing or sharing it. Do not put real credentials or private customer data in prompts or the committed log.

To verify runtime execution, start a **new** chat session and submit a harmless prompt. The log entry may briefly have an empty response while the turn is in progress; after the agent finishes, `agentStop` should fill in the response. Check the VS Code hook output for command errors and verify that the repository is trusted and the selected harness supports Copilot-compatible hooks. `/hooks` confirms discovery and enablement, not command execution.

## Validate the logger

Run `python3 -m unittest discover -s .github/hooks -p 'test_*.py'`. The tests cover prompt and response hook configuration, transcript extraction, the two-field record shape, redaction, rejection of non-prompt events, and completing a logged prompt with its response.
