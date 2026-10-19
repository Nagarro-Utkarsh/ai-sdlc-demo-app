import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from interaction_log import record_for, redact


class InteractionLogTests(unittest.TestCase):
    def test_hook_configuration_uses_copilot_event_names(self):
        config_path = Path(__file__).with_name("interaction-log.json")
        config = json.loads(config_path.read_text(encoding="utf-8"))

        self.assertEqual(1, config["version"])
        self.assertEqual(
            {
                "userPromptSubmitted",
                "postToolUse",
                "postToolUseFailure",
                "agentStop",
                "subagentStart",
                "subagentStop",
            },
            set(config["hooks"]),
        )
        for hooks in config["hooks"].values():
            self.assertTrue(all(hook["type"] == "command" for hook in hooks))

    def test_redacts_sensitive_fields_and_inline_values(self):
        value = {
            "password": "ChangeMe123!",
            "prompt": 'Set token="abc123" and use Bearer abc.def in the header.',
            "command": "curl --api-key secret-value https://user:pass@example.com",
        }

        redacted = redact(value)

        self.assertEqual("[REDACTED]", redacted["password"])
        self.assertNotIn("ChangeMe123!", repr(redacted))
        self.assertNotIn("abc123", repr(redacted))
        self.assertNotIn("abc.def", repr(redacted))
        self.assertNotIn("secret-value", repr(redacted))
        self.assertNotIn("user:pass", repr(redacted))

    def test_records_stable_copilot_tool_event_payload(self):
        record = record_for(
            {
                "hook_event_name": "postToolUse",
                "session_id": "session-1",
                "timestamp": "2026-10-19T00:00:00Z",
                "tool_name": "read_file",
                "tool_input": {"path": "README.md"},
                "tool_result": {"text_result_for_llm": "read complete"},
            }
        )

        self.assertEqual("GitHub Copilot", record["provider"])
        self.assertEqual("read_file", record["tool_name"])
        self.assertEqual("read complete", record["tool_result"]["text_result_for_llm"])

    def test_records_copilot_prompt_event_name(self):
        record = record_for(
            {
                "hook_event_name": "userPromptSubmitted",
                "sessionId": "session-2",
                "prompt": 'Check approval limits; token="abc123".',
            }
        )

        self.assertEqual("UserPromptSubmit", record["event_type"])
        self.assertEqual("user", record["agent_role"])
        self.assertEqual('Check approval limits; token="[REDACTED]".', record["prompt"])
        self.assertNotIn("abc123", record["prompt"])

    def test_hook_command_appends_copilot_prompt_to_repository_log(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".git").mkdir()
            event = {
                "hook_event_name": "userPromptSubmitted",
                "prompt": "Summarize the approval limits.",
                "cwd": str(root),
            }
            script = Path(__file__).with_name("interaction_log.py")

            result = subprocess.run(
                [sys.executable, str(script)],
                input=json.dumps(event),
                text=True,
                capture_output=True,
                cwd=root,
                check=False,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            records = [
                json.loads(line)
                for line in (root / "AI_INTERACTION_LOG.jsonl").read_text().splitlines()
            ]
            self.assertEqual(1, len(records))
            self.assertEqual("UserPromptSubmit", records[0]["event_type"])
            self.assertEqual("Summarize the approval limits.", records[0]["prompt"])

    def test_records_manual_prompt_with_capture_method(self):
        record = record_for(
            {
                "event_type": "UserPromptSubmit",
                "prompt": "Please continue; password=example123",
                "capture_method": "manual from visible conversation; not a live hook",
            }
        )

        self.assertEqual("UserPromptSubmit", record["event_type"])
        self.assertEqual("user", record["agent_role"])
        self.assertEqual('Please continue; password="[REDACTED]"', record["prompt"])
        self.assertEqual(
            "manual from visible conversation; not a live hook",
            record["capture_method"],
        )

    def test_records_copilot_agent_stop_event_name(self):
        record = record_for(
            {
                "hook_event_name": "agentStop",
                "transcriptPath": "/tmp/session.jsonl",
                "stopReason": "completed",
            }
        )

        self.assertEqual("Stop", record["event_type"])
        self.assertTrue(record["transcript_available"])
        self.assertNotIn("response", record)

    def test_records_documented_manual_assistant_response_event_type(self):
        record = record_for(
            {
                "event_type": "ManualAssistantResponse",
                "response": "Approval tests pass.",
                "capture_method": "manual Copilot chat record",
            }
        )

        self.assertEqual("ManualAssistantResponse", record["event_type"])
        self.assertEqual("Approval tests pass.", record["response"])
        self.assertEqual("manual Copilot chat record", record["capture_method"])

    def test_stop_records_limit_without_claiming_response_capture(self):
        record = record_for({"hook_event_name": "Stop", "transcript_path": "/tmp/session.jsonl"})

        self.assertTrue(record["transcript_available"])
        self.assertNotIn("response", record)
        self.assertIn("not assistant response text", record["response_capture"])


if __name__ == "__main__":
    unittest.main()
