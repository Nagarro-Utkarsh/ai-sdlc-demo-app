import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from interaction_log import record_for, redact, transcript_exchange


class InteractionLogTests(unittest.TestCase):
    def test_hook_configuration_records_prompts_and_completed_responses(self):
        config_path = Path(__file__).with_name("interaction-log.json")
        config = json.loads(config_path.read_text(encoding="utf-8"))

        self.assertEqual(1, config["version"])
        self.assertEqual({"userPromptSubmitted", "agentStop"}, set(config["hooks"]))
        for event_name in ("userPromptSubmitted", "agentStop"):
            hook = config["hooks"][event_name][0]
            self.assertEqual("command", hook["type"])
            self.assertTrue(hook["bash"].endswith(f"interaction_log.py {event_name}"))
            self.assertTrue(hook["powershell"].endswith(f"interaction_log.py {event_name}"))

    def test_redacts_sensitive_fields_and_inline_values(self):
        value = {
            "password": "ChangeMe123!",
            "prompt": 'Set token="abc123" and use ****** in the header.',
            "command": "curl --api-key secret-value https://user:pass@example.com",
        }

        redacted = redact(value)

        self.assertEqual("[REDACTED]", redacted["password"])
        self.assertNotIn("ChangeMe123!", repr(redacted))
        self.assertNotIn("abc123", repr(redacted))
        self.assertNotIn("secret-value", repr(redacted))
        self.assertNotIn("user:pass", repr(redacted))

    def test_redacts_bearer_and_url_credentials(self):
        redacted = redact("Bearer abc.def https://user:pass@example.com")

        self.assertNotIn("abc.def", redacted)
        self.assertNotIn("user:pass", redacted)

    def test_records_copilot_prompt_with_empty_response(self):
        record = record_for(
            {
                "hook_event_name": "userPromptSubmitted",
                "prompt": 'Check approval limits; token="abc123".',
            }
        )

        self.assertEqual(
            {"prompt": 'Check approval limits; token="[REDACTED]".', "response": ""},
            record,
        )
        self.assertNotIn("abc123", record["prompt"])

    def test_records_prompt_and_response_when_both_are_present(self):
        record = record_for(
            {
                "hook_event_name": "userPromptSubmitted",
                "prompt": "Explain the approval threshold.",
                "response": 'Manager approvals cap at 50,000; token="abc123".',
            }
        )

        self.assertEqual(
            {
                "prompt": "Explain the approval threshold.",
                "response": 'Manager approvals cap at 50,000; token="[REDACTED]".',
            },
            record,
        )
        self.assertNotIn("abc123", record["response"])

    def test_reads_latest_exchange_from_jsonl_transcript(self):
        with tempfile.TemporaryDirectory() as directory:
            transcript_path = Path(directory) / "transcript.jsonl"
            transcript_path.write_text(
                "\n".join(
                    [
                        json.dumps({"type": "user.message", "data": {"content": "Earlier prompt"}}),
                        json.dumps({"type": "assistant.message", "data": {"content": "Earlier response"}}),
                        json.dumps({"type": "user.message", "user_content": "What is the approval limit?"}),
                        json.dumps(
                            {
                                "type": "assistant.message",
                                "assistant_content": "The Manager limit is 50,000.",
                            }
                        ),
                    ]
                )
                + "\n",
                encoding="utf-8",
            )

            self.assertEqual(
                ("What is the approval limit?", "The Manager limit is 50,000."),
                transcript_exchange(transcript_path),
            )

    def test_uses_configured_prompt_event_name_when_payload_omits_it(self):
        record = record_for({"prompt": "Record this prompt."}, "userPromptSubmitted")

        self.assertEqual({"prompt": "Record this prompt.", "response": ""}, record)

    def test_rejects_non_prompt_events(self):
        with self.assertRaisesRegex(ValueError, "Unsupported Copilot hook event"):
            record_for({"hook_event_name": "postToolUse", "prompt": "Do not log tools."})

    def test_requires_prompt_text(self):
        with self.assertRaisesRegex(ValueError, "must include a string prompt"):
            record_for({"hook_event_name": "userPromptSubmitted"})

    def test_vscode_hook_commands_append_prompt_then_update_it_at_stop(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".git").mkdir()
            event = {
                "prompt": "Summarize the approval limits.",
                "cwd": str(root),
            }
            script = Path(__file__).with_name("interaction_log.py")

            result = subprocess.run(
                [sys.executable, str(script), "userPromptSubmitted"],
                input=json.dumps(event),
                text=True,
                capture_output=True,
                cwd=root,
                check=False,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            transcript_path = root / "transcript.jsonl"
            transcript_path.write_text(
                "\n".join(
                    [
                        json.dumps(
                            {
                                "type": "user.message",
                                "user_content": "Summarize the approval limits. ",
                            }
                        ),
                        json.dumps(
                            {
                                "type": "assistant.message",
                                "assistant_content": 'Managers can approve invoices up to 50,000; token="private-value".',
                            }
                        ),
                    ]
                )
                + "\n",
                encoding="utf-8",
            )
            response_result = subprocess.run(
                [sys.executable, str(script), "agentStop"],
                input=json.dumps(
                    {
                        "agent_transcript_path": str(transcript_path),
                        "cwd": str(root),
                    }
                ),
                text=True,
                capture_output=True,
                cwd=root,
                check=False,
            )

            self.assertEqual(0, response_result.returncode, response_result.stderr)
            records = [
                json.loads(line)
                for line in (root / "AI_INTERACTION_LOG.jsonl").read_text(encoding="utf-8").splitlines()
            ]
            self.assertEqual(
                [
                    {
                        "prompt": "Summarize the approval limits.",
                        "response": 'Managers can approve invoices up to 50,000; token="[REDACTED]".',
                    }
                ],
                records,
            )

    def test_vscode_hook_records_direct_assistant_response_without_transcript(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / ".git").mkdir()
            script = Path(__file__).with_name("interaction_log.py")
            prompt_result = subprocess.run(
                [sys.executable, str(script), "userPromptSubmitted"],
                input=json.dumps(
                    {
                        "prompt": "Explain the approval threshold.",
                        "cwd": str(root),
                    }
                ),
                text=True,
                capture_output=True,
                cwd=root,
                check=False,
            )
            self.assertEqual(0, prompt_result.returncode, prompt_result.stderr)

            response_result = subprocess.run(
                [sys.executable, str(script), "agentStop"],
                input=json.dumps(
                    {
                        "last_assistant_message": "Managers can approve up to 50,000.",
                        "cwd": str(root),
                    }
                ),
                text=True,
                capture_output=True,
                cwd=root,
                check=False,
            )

            self.assertEqual(0, response_result.returncode, response_result.stderr)
            records = [
                json.loads(line)
                for line in (root / "AI_INTERACTION_LOG.jsonl").read_text(encoding="utf-8").splitlines()
            ]
            self.assertEqual(
                [
                    {
                        "prompt": "Explain the approval threshold.",
                        "response": "Managers can approve up to 50,000.",
                    }
                ],
                records,
            )


if __name__ == "__main__":
    unittest.main()
