# Tasks

## 1. Single-Approver Limits

- [x] 1.1 Add Manager and Director role-aware approval input and enforce the 50,000 and 500,000 single-approval limits; verify `dotnet test backend/InvoiceApp.sln` covers 50,000, 50,000.01, 500,000, and 500,000.01.
- [x] 1.2 Update demo approval users and API authorization for Manager and Director while keeping Admin's invoice-administration and rejection access; verify authorization behavior with an observed HTTP check.
- [x] 1.3 Return clear limit errors before changing invoice fields; verify negative unit tests compare the full invoice state before and after a rejected single approval.

## 2. High-Value Two-Approver Flow

- [x] 2.1 Record a Manager's first approval for amounts above 500,000 while leaving the invoice pending; verify a unit test checks the first approver and unchanged final-decision fields.
- [x] 2.2 Require a distinct Director as the second approver and reject a Director-first attempt; verify unit tests cover the valid sequence, same-user attempt, and wrong-role attempts.
- [x] 2.3 Ensure every rejected second-approval attempt leaves the complete invoice unchanged and surfaces a clear API/UI reason; verify with state-snapshot assertions and the Playwright rejection-flow test.
- [x] 2.4 Add one Playwright test for a rejected over-limit approval and verify the alert is visible and the invoice state is unchanged through the UI.

## 3. Integration Evidence

- [x] 3.1 Run `dotnet test backend/InvoiceApp.sln` and the documented Playwright command; verify both results are recorded with the exact commands and outcomes.
- [x] 3.2 Run the pinned Stryker 4.13.0 mutation command after recording a baseline; verify the before/after scores and surviving mutants are documented.

## Workflow follow-up

- Archive the OpenSpec change after implementation review and acceptance.
- Record the final artifact and verification outcomes in the assignment checklist and interaction log.
