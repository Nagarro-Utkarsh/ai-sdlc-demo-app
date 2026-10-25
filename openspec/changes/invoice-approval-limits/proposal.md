# Proposal

## Why

The current approval service finalizes every pending invoice after one approval, regardless of amount or actor role. This change adds the approval limits and additional control required by the assignment while making failed approval attempts safe to retry.

## What Changes

- Add Manager and Director approval limits for single-approver invoices.
- Require a Manager first and a different Director second for invoices above 500,000.
- Support partial approval with a required note and apply role limits to the approved amount while retaining the original invoice amount.
- Return a clear reason for an invalid approval attempt without mutating the invoice.
- Add unit and browser-level coverage for the limits and rejected-attempt behavior.

## Capabilities

### New Capabilities

- `invoice-approval`: Role-based approval limits and multi-approver handling for invoices.

### Modified Capabilities

None.

## Impact

This affects approval lifecycle state in the Domain layer, the invoice model, API approval requests, demo user configuration, the approval UI, and unit and Playwright tests. The 500,000 two-approver threshold is evaluated using the approved amount. For approved amounts above 500,000, the first approver is a Manager and the second approver is a different Director.
