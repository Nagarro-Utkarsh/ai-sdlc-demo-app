# AI interaction log

The assignment log is `AI_INTERACTION_LOG.jsonl` at the repository root. Open this repository as the VS Code workspace, trust it, and select the GitHub Copilot harness. The versioned Copilot hook configuration uses the lower-camel event names required by Copilot-compatible hooks.

The hooks append JSONL records for submitted prompts, successful and failed tool calls, agent stop events, and custom subagent starts/results. Copilot's lower-camel event names are normalized to the logger's stable event names. The logger redacts values under sensitive field names, common inline secret assignments, bearer values, secret-bearing CLI flags, and URL credentials. The log is local repository content and must be reviewed for secrets before committing or sharing it.

## Response-capture limit

The stable main-agent `Stop` hook payload contains a transcript path and stop reason, not the assistant's final response text. The logger intentionally records that limitation instead of parsing the unstable transcript-file format or claiming complete response capture. Subagent completion payloads do expose the final response, which is recorded after redaction.

For a complete assignment hand-in, export the Copilot chat transcript using the VS Code chat export capability, review it for private data, and append any required assistant response text or summaries using `/record-assistant-response`. Pipe the resulting single-line JSON object with `event_type: "ManualAssistantResponse"` into `python3 .github/hooks/interaction_log.py`; the logger applies the same redaction and appends it. Label manually added entries as manual; never reconstruct hidden reasoning or fabricate missing turns. Hook capture only starts after the repository is opened in a VS Code Copilot harness with the hooks enabled and trusted.

## Validate the logger

Run `python3 -m unittest discover -s .github/hooks -p 'test_*.py'`. The tests cover sensitive-value redaction, the stable Copilot tool-event shape, documented manual assistant-response records, and the explicit main-response limitation. Do not put real credentials or private customer data in prompts or the committed log.
