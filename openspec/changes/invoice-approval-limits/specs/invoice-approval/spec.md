# Spec Delta

## Purpose

Defines role-based invoice approval limits and the required approval checks so users can make valid decisions while rejected attempts leave invoice data intact.

## ADDED Requirements

### Requirement: Manager single-approval limit
The system SHALL allow a Manager to approve a pending invoice with an amount of 50,000 or less using one approval.

#### Scenario: Manager approves at the limit
- **WHEN** a Manager approves a pending invoice for 50,000
- **THEN** the invoice is approved

#### Scenario: Manager attempts a single approval above the limit
- **WHEN** a Manager attempts to approve a pending invoice for more than 50,000 with one approval
- **THEN** the attempt is rejected with a clear reason and the invoice remains unchanged

#### Scenario: Manager cannot singly approve a mid-value invoice
- **WHEN** a Manager attempts one approval for an invoice above 50,000 and at or below 500,000
- **THEN** the attempt is rejected and the invoice remains unchanged

### Requirement: Director single-approval limit
The system SHALL allow a Director to approve a pending invoice with an amount of 500,000 or less using one approval.

#### Scenario: Director approves at the limit
- **WHEN** a Director approves a pending invoice for 500,000
- **THEN** the invoice is approved

#### Scenario: Director attempts a single approval above the limit
- **WHEN** a Director attempts to approve an invoice above 500,000 without a prior Manager approval
- **THEN** the attempt is rejected with a clear reason and the invoice remains unchanged

### Requirement: Two approvals for high-value invoices
The system SHALL require a Manager to record the first approval and a Director with a different authenticated username to record the second approval for an invoice above 500,000 before approving the invoice. The Manager's first approval SHALL leave the invoice pending, and the Director's second approval SHALL complete it.

#### Scenario: Manager records the first high-value approval
- **WHEN** a Manager approves an invoice above 500,000
- **THEN** the invoice remains pending for a second approval and records the Manager as the first approver

#### Scenario: Different Director completes a high-value approval
- **WHEN** a Director with a different authenticated username records the second approval after a Manager first approval
- **THEN** the system records the Director identity and approval time, marks the invoice approved, and records the Director and time as the final decision

#### Scenario: Manager first approval is not a final decision
- **WHEN** the first Manager approval is recorded for an invoice above 500,000
- **THEN** the invoice remains pending, records the Manager identity and approval time, and leaves final decision fields unset

#### Scenario: First approval is visible as awaiting Director approval
- **WHEN** the Manager's first approval is recorded
- **THEN** the invoice remains in the pending approvals list with an indication that a different Director must approve it next

#### Scenario: Same identity attempts both approvals
- **WHEN** the user who recorded the first approval attempts to record the second approval for that invoice
- **THEN** the attempt is rejected with a clear reason and the invoice remains unchanged

#### Scenario: Non-Director attempts the second approval
- **WHEN** a non-Director attempts to record the second approval for an invoice above 500,000
- **THEN** the attempt is rejected with a clear reason and the invoice remains unchanged

#### Scenario: Director attempts the first approval
- **WHEN** a Director attempts to record the first approval for an invoice above 500,000
- **THEN** the attempt is rejected with a clear reason and the invoice remains unchanged

#### Scenario: Manager cannot complete the second step
- **WHEN** a different Manager attempts the second approval for an invoice above 500,000
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
