---
name: invoice-approval-workflow
description: "Use when planning, implementing, testing, or reviewing the invoice approval assignment in this repository."
argument-hint: "The implementation slice or deliverable to work on"
---

# Invoice approval assignment workflow

Use this workflow for the role-limit feature and its assignment evidence. Keep the approved behavior in `openspec/changes/invoice-approval-limits/`; keep repository-wide facts in `AGENTS.md`.

## Work one slice at a time

1. Read the relevant acceptance criteria and current task checklist.
2. Ask `approval-test-designer` for a focused adversarial test matrix before implementing a non-trivial rule.
3. Implement only the requested slice. The single-approver thresholds and the >500,000 two-person flow must remain independently reviewable.
4. Assert complete invoice snapshots for rejected attempts, including approval identities/timestamps and decision fields.
5. Run the focused tests and `dotnet test backend/InvoiceApp.sln`; report exact commands and results.
6. Ask `approval-reviewer` to independently review the changed diff. Resolve actionable findings before marking tasks complete.
7. Update the OpenSpec checklist only after behavior and tests pass. Never claim mutation scores, PR status, a human review, or an uploaded video without evidence.

## Assignment deliverables

Use the configured Copilot hooks and agents, preserve the structured interaction log, add one Playwright rejection-flow test, record comparable mutation evidence, and keep `ASSIGNMENT.md` aligned with actual evidence. For human-only work, provide a precise handoff rather than inventing results.
