# Spec Delta

## Purpose

Defines role-based invoice approval limits and the required approval checks so users can make valid decisions while rejected attempts leave invoice data intact.

## ADDED Requirements

### Requirement: Partial approval amount and note
The system SHALL retain the original invoice amount and record the approved amount separately. An approval amount SHALL be greater than zero and no greater than the original amount. An approval amount lower than the original amount SHALL require a note. The approved amount and note SHALL be visible with the invoice after approval.

#### Scenario: Partial approval records amount and note
- **WHEN** an approver approves less than the invoiced amount with a note
- **THEN** the invoice records both amounts, the note, and a PartiallyApproved status

#### Scenario: Partial approval requires a note
- **WHEN** an approver submits an amount lower than the invoiced amount without a note
- **THEN** the attempt is rejected and the invoice remains unchanged

#### Scenario: Invalid approval amount is rejected
- **WHEN** an approver submits an amount that is zero, negative, or greater than the invoiced amount
- **THEN** the attempt is rejected and the invoice remains unchanged

### Requirement: Manager single-approval limit
The system SHALL allow a Manager to approve an amount of 50,000 or less using one approval.

#### Scenario: Manager approves at the limit
- **WHEN** a Manager approves a pending invoice for an approved amount of 50,000
- **THEN** the invoice is approved, or partially approved if the invoiced amount is higher

#### Scenario: Manager attempts a single approval above the limit
- **WHEN** a Manager attempts to approve an amount above 50,000 with one approval
- **THEN** the attempt is rejected with a clear reason and the invoice remains unchanged

#### Scenario: Manager cannot singly approve a mid-value invoice
- **WHEN** a Manager attempts one approval for an invoice above 50,000 and at or below 500,000
- **THEN** the attempt is rejected and the invoice remains unchanged

### Requirement: Director single-approval limit
The system SHALL allow a Director to approve an amount of 500,000 or less using one approval.

#### Scenario: Director approves at the limit
- **WHEN** a Director approves a pending invoice for an approved amount of 500,000
- **THEN** the invoice is approved, or partially approved if the invoiced amount is higher

#### Scenario: Director attempts a single approval above the limit
- **WHEN** a Director attempts to approve an amount above 500,000 without a prior Manager approval
- **THEN** the attempt is rejected with a clear reason and the invoice remains unchanged

### Requirement: Two approvals for high-value approved amounts
The system SHALL require a Manager to record the first approval and a Director with a different authenticated username to record the second approval when the approved amount is above 500,000. The Manager's first approval SHALL leave the invoice pending and record the approved amount and note. The Director SHALL confirm the same amount before completing approval.

#### Scenario: Manager records the first high-value approval
- **WHEN** a Manager approves an amount above 500,000
- **THEN** the invoice remains pending for a second approval and records the Manager as the first approver

#### Scenario: Different Director completes a high-value approval
- **WHEN** a Director with a different authenticated username confirms the amount recorded by a Manager
- **THEN** the system records the Director identity and approval time, marks the invoice approved or partially approved according to the two amounts, and records the Director and time as the final decision

#### Scenario: Director can singly approve a partial amount below 500,000
- **WHEN** a Director approves an amount of 500,000 or less on an invoice whose original amount is above 500,000
- **THEN** the Director's single approval completes the partial approval

#### Scenario: Director cannot change the amount in the second step
- **WHEN** a Director submits an amount different from the amount recorded by the Manager
- **THEN** the attempt is rejected and the invoice remains unchanged

#### Scenario: Manager first approval is not a final decision
- **WHEN** the first Manager approval is recorded for an approved amount above 500,000
- **THEN** the invoice remains pending, records the Manager identity and approval time, and leaves final decision fields unset

#### Scenario: First approval is visible as awaiting Director approval
- **WHEN** the Manager's first approval is recorded
- **THEN** the invoice remains in the pending approvals list with an indication that a different Director must approve it next

#### Scenario: Same identity attempts both approvals
- **WHEN** the user who recorded the first approval attempts to record the second approval for that invoice
- **THEN** the attempt is rejected with a clear reason and the invoice remains unchanged

#### Scenario: Non-Director attempts the second approval
- **WHEN** a non-Director attempts to record the second approval for an approved amount above 500,000
- **THEN** the attempt is rejected with a clear reason and the invoice remains unchanged

#### Scenario: Director attempts the first approval
- **WHEN** a Director attempts to record the first approval for an approved amount above 500,000
- **THEN** the attempt is rejected with a clear reason and the invoice remains unchanged

#### Scenario: Manager cannot complete the second step
- **WHEN** a different Manager attempts the second approval for an approved amount above 500,000
- **THEN** the attempt is rejected with a clear reason and the invoice remains unchanged

### Requirement: Rejected approval attempts preserve invoice state
The system SHALL return a clear reason for a rejected approval attempt and SHALL leave all invoice state unchanged by that attempt.

#### Scenario: Rejected attempt does not alter invoice state
- **WHEN** an approval attempt violates an approval rule
- **THEN** the system returns a clear reason and preserves the invoice status, approval identities, decision timestamps, and other invoice fields

#### Scenario: Rejecting after the Manager's first approval preserves that approval record
- **WHEN** an authorized user rejects an invoice after its first Manager approval
- **THEN** the invoice is rejected, the rejecting user and time are recorded as the final decision, and the Manager's first approval record remains intact

### Requirement: Approval endpoint roles
The system SHALL allow Managers and Directors to approve invoices. Admins SHALL NOT satisfy an approval step, and an approval attempt from an unauthorized role SHALL return a clear reason without changing the invoice.

#### Scenario: Admin cannot approve an invoice
- **WHEN** an Admin attempts to approve a pending invoice
- **THEN** the system returns a clear authorization reason and leaves the invoice unchanged

#### Scenario: Manager or Director may reject a pending invoice
- **WHEN** a Manager or Director rejects a pending invoice
- **THEN** the invoice is rejected with the reason and final decision fields recorded

#### Scenario: Admin may reject but cannot approve
- **WHEN** an Admin rejects a pending invoice
- **THEN** the invoice is rejected with the reason and final decision fields recorded
