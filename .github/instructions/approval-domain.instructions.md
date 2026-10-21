---
description: "Use when changing invoice approval domain rules, API role authorization, or approval state."
applyTo: "backend/src/InvoiceApp.Domain/**/*.cs,backend/src/InvoiceApp.Api/**/*.cs"
---

- Keep approval policy in `InvoiceApp.Domain`; pass the authenticated username and role from the API.
- Treat 50,000 and 500,000 as inclusive single-approval limits for Manager and Director respectively.
- For amounts above 500,000, require a Manager first and a different Director second; keep the invoice pending after the first step.
- Validate every rejection condition before mutating the invoice. Test that rejected attempts preserve the complete state.
- Keep Admin out of approval steps, while preserving the specified administration and rejection permissions.
- Add or update focused tests for every behavior and keep HTTP/UI error messages actionable.
