# Design

## Context

See `proposal.md` for motivation and `specs/invoice-approval/spec.md` for the behavior contract. `ApprovalService` currently finalizes any pending invoice on one call and receives the username without the user's role. The `Invoice` model stores one decision identity and timestamp. The API keeps demo users and invoices in memory, and the UI already presents approval failures in an alert region. There is no database migration.

## Goals / Non-Goals

**Goals:**
- Keep approval policy in the Domain service and pass authenticated username and role from the API.
- Support full and partial approvals while retaining the invoiced amount and the final approved amount separately.
- Require a note for partial approvals and expose the approval amount and note in the UI.
- Record a Manager's first high-value approval without finalizing the invoice, then finalize only after a different Director approves.
- Keep rejected attempts free of partial invoice mutation.
- Cover each slice with unit tests and add one browser test for a rejected approval.

**Non-Goals:**
- Add persistent storage, configurable limits, or a general workflow engine.
- Change invoice submission or rejection policy beyond the authorization required to use the approval endpoints.

## Decisions

- Pass both role and username to the Domain approval operation. This keeps role thresholds in the business layer and avoids trusting a role supplied in the request body.
- Accept an optional approved amount and note on the approval request. An omitted amount means the original invoice amount, preserving full-approval calls. Validate that the amount is positive and does not exceed the original amount; require a note when it is lower.
- Apply the 50,000 and 500,000 limits and the two-approver threshold to the requested approved amount, not the original invoice amount. A Director may singly approve a partial amount of 500,000 or less even when the original invoice exceeds 500,000.
- For a high-value approval amount, record the Manager's amount and note with the first approval. Require the distinct Director to confirm the same amount before finalizing. Keep the original amount unchanged and record the final amount in `ApprovedAmount`.
- Represent approval progress on the invoice with explicit first- and second-approval identity, role, and timestamp fields. Use the authenticated username as the demo app's identity key. Keep final decision fields for completed approve/reject outcomes; for approval, the Director's second approval is the final decision identity/time. A valid rejection after the first approval retains the first-approval fields and records the rejecting user as the final decision identity/time.
- Validate invoice status, amount, required role, and identity distinctness before changing any approval or decision fields. A failed validation returns the existing service error path without mutation.
- Above 500,000, stage rules take precedence over the single-approval limits: only a Manager can record the first step, only a different Director can complete it; a Manager above 50,000 through 500,000 and a Director above 500,000 cannot finalize with one approval. Exactly 500,000 is eligible for a single Director approval; 500,000.01 requires both steps.
- Use `Manager` and `Director` for approval actions. Seed demo accounts for both. Keep `Admin` able to administer invoices and reject pending invoices, but do not let Admin satisfy an approval step. Return a clear authorization reason when Admin attempts approval.
- Deliver the feature in two implementation commits: single-approver limits first, then the high-value two-approver flow. Keep tests for each behavior in its implementation slice.

## Risks / Trade-offs

- [Existing clients or tests may depend on `Approver`/`Admin` approval authorization] → Update demo configuration and tests together; preserve Admin's invoice administration permission, and document the role change in the UI context.
- [Approval fields could be partially updated before a later check fails] → Perform all rejection checks before mutation and assert a complete invoice snapshot is unchanged in negative tests.
- [No persistence exists] → Keep the solution in the current in-memory model and avoid implying approvals survive process restart.

## Migration Plan

No data migration is needed because the application uses in-memory storage. Replace the seeded approval demo account with Manager and Director examples. Rollback is a source revert.
