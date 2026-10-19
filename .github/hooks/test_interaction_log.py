import unittest

from interaction_log import record_for, redact


class InteractionLogTests(unittest.TestCase):
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
                "hook_event_name": "PostToolUse",
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
