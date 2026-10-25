---
name: review-approval-change
description: "Independently review the current invoice approval diff for behavioral defects and missing evidence."
argument-hint: "Optional scope, such as staged changes or the current PR diff"
agent: approval-reviewer
---

Review this scope: ${input:scope}

Read the relevant source and tests, compare with `AGENTS.md`, the OpenSpec acceptance criteria, and the assignment. Do not edit. Report only actionable findings with severity, file/line, impact, evidence, confidence, and a minimal next step. State what was reviewed and which checks you ran.
