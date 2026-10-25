---
description: "Use when adding or changing Playwright end-to-end tests for the invoice approval UI."
applyTo: "e2e/**/*.ts"
---

- Use Playwright page objects and accessible role/label locators; do not use CSS or XPath selectors.
- Exercise the real UI from sign-in through the decision attempt.
- Assert the visible rejection reason and verify the invoice is still pending after refresh.
- Run the configured Playwright test command and report environment or browser-install blockers explicitly.
