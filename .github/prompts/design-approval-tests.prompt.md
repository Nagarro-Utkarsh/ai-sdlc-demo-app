---
name: design-approval-tests
description: "Create an adversarial test matrix for a specified invoice approval rule or code change."
argument-hint: "The rule or changed code to test"
agent: approval-test-designer
---

Design tests for this requested scope: ${input:scope}

Read `AGENTS.md` and the active OpenSpec change first. Return concrete test names, initial invoice state, role and identity, action, assertions, and the acceptance criterion each test proves. Include exact-boundary cases and rejected-state snapshots where relevant. Do not edit files.
