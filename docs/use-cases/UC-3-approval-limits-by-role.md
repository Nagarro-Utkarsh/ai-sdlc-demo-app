# UC-3: Approval limits by role

## Background

Invoices need approval controls that match the authority of the person making the decision. A rejected approval attempt must explain why it failed without changing the invoice.

## The change

Apply approval limits to the amount being approved. Managers may approve up to 50,000 in one step. Directors may approve up to 500,000 in one step. An amount above 500,000 requires a Manager's first approval and a different Director's second approval.

## What the business asked for

- The 50,000 Manager limit and 500,000 Director limit are inclusive.
- Amounts above 500,000 stay pending after the Manager's first approval until a distinct Director confirms the same approved amount.
- The same user cannot count as both approvers.
- A Director cannot be the first approver for an amount above 500,000, and a Manager cannot provide the second approval.
- The approved amount determines which limit applies. Partial approvals retain the original invoice amount and require a note.
- Every rejected approval returns a clear reason and leaves all invoice fields unchanged.
- Admins may reject pending invoices but cannot approve them.

## Where it touches the code

Approval policy belongs in `backend/src/InvoiceApp.Domain/ApprovalService.cs`. API role checks live in `backend/src/InvoiceApp.Api/Controllers/ApprovalsController.cs`; approval UI behavior lives under `backend/src/InvoiceApp.Api/wwwroot/`; unit and browser tests cover the decisions and rejected flows.

The detailed acceptance scenarios are maintained in `openspec/changes/invoice-approval-limits/specs/invoice-approval/spec.md`.
