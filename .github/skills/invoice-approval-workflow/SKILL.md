---
name: repository-development-workflow
description: "Use when planning, implementing, testing, or reviewing code changes in this repository."
argument-hint: "The change or deliverable to work on"
---

# Repository development workflow

Use this workflow for repository changes. Read `AGENTS.md` first, then consult the relevant source, tests, and feature documentation. Keep repository-wide conventions in `AGENTS.md` and feature acceptance criteria in the relevant OpenSpec change or use-case brief.

## Plan and implement

1. Identify the relevant acceptance criteria, existing behavior, and affected layers before editing.
2. Make the smallest complete change that satisfies the request, keeping business rules in the Domain layer and transport or presentation concerns in their existing layers.
3. Follow the repository conventions and add or update focused tests for new rules and endpoints.
4. When verification is requested, run the narrowest relevant checks first, then broader checks as appropriate. Report the exact commands and results.
5. Review the final diff for correctness, authorization, state preservation, documentation accuracy, and unrelated changes.
6. Update OpenSpec tasks or other progress records only after the corresponding work is complete. Never claim test, mutation, review, or publication evidence that was not observed.

For approval changes, follow the role limits and state-transition rules in `AGENTS.md` and the relevant invoice approval spec. For other changes, apply the corresponding feature requirements and repository conventions.
