# Invoice approval assignment write-up

## Scope and result
This repository implements role-based full and partial invoice approvals. The approval service enforces the single-approver limits and the two-step flow for high approved amounts, while rejecting invalid approval attempts without mutating invoice state.

The main files involved are:
- `backend/src/InvoiceApp.Domain/ApprovalService.cs` — approval rules and lifecycle transitions
- `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs` — unit tests for limits, approval order, and same-user rejection cases
- `e2e/tests/approval-rejection.spec.ts` — end-to-end coverage of approval limits, partial approval, rejection, and the two-approver workflow
- `AGENTS.md` — contributor instructions and required workflow expectations
- `openspec/changes/invoice-approval-limits/` — spec, design, and task breakdown
- `AI_INTERACTION_LOG.jsonl` — structured AI interaction records

## Implemented behaviour
- Manager single-approval limit: 50,000 inclusive
- Director single-approval limit: 500,000 inclusive
- Partial approvals retain the original invoice amount, record the approved amount separately, and require a note
- Role limits and the two-approver threshold use the approved amount
- Approved amounts above 500,000 require a Manager approval followed by a different Director approval of the same amount
- A partial approval is shown separately from a full approval
- The same user cannot appear as both approvers on the same invoice
- A rejected attempt returns a clear reason and leaves the invoice unchanged

Bulk approval is documented as a separate use case, but is not implemented in this app and is not covered by these tests.

## Verification commands and evidence

Verification includes the approval-limit, partial-approval, rejection, and two-approver workflows. The backend unit suite and Playwright end-to-end suite both pass.

1. Unit tests
```bash
cd ai-sdlc-demo-app
dotnet test backend/InvoiceApp.sln --nologo --verbosity minimal
```
Result:
- Passed: 27
- Failed: 0
- Skipped: 0

2. End-to-end test
```bash
cd ai-sdlc-demo-app/e2e
npm test
```
Result:
- 9 tests run
- 9 passed
- 0 failed
- Covers Manager and Director limits, a distinct Manager-then-Director approval, over-limit and invalid-approval state preservation, partial approvals and required notes, and role-based rejection.
The E2E server uses port 5055 because macOS AirTunes already occupies port 5000 on this machine.

3. API authorization smoke test
Using the local API at `http://127.0.0.1:5058`, demo-user logins succeeded and the following requests were observed:
- Manager `POST /api/approvals/INV-1001/approve`: HTTP 200; invoice became `Approved`.
- Admin `POST /api/approvals/INV-1002/approve`: HTTP 403.
- Admin `POST /api/approvals/INV-1002/reject` with a reason: HTTP 200.

4. API build
```bash
cd ai-sdlc-demo-app
dotnet build backend/src/InvoiceApp.Api/InvoiceApp.Api.csproj --nologo --verbosity minimal
```
Result: build succeeded with 0 warnings and 0 errors.

5. Mutation testing

Stryker 4.13.0 was run from `backend/tests/InvoiceApp.Tests`, scoped to `ApprovalService.cs`:

```bash
dotnet stryker --project InvoiceApp.Domain.csproj --mutate "**/ApprovalService.cs"
```

Result: **87.96% mutation score** — 95 killed, 6 survived, 7 no coverage.

## AI log
The assignment log is maintained at:
- `AI_INTERACTION_LOG.jsonl`

The prompt and response logging hooks are configured in `.github/hooks/interaction-log.json` and implemented in:
- `.github/hooks/interaction_log.py`

## Demo video
[nagp-ai-in-sdlc-assignment.mp4](https://nagarro-my.sharepoint.com/:v:/p/utkarsh_mahajan/IQBqPAkS_nGuTLLlLiD-cNPyAR1ACk0giRb1HzH2txszOoY?nav=eyJyZWZlcnJhbEluZm8iOnsicmVmZXJyYWxBcHAiOiJPbmVEcml2ZUZvckJ1c2luZXNzIiwicmVmZXJyYWxBcHBQbGF0Zm9ybSI6IldlYiIsInJlZmVycmFsTW9kZSI6InZpZXciLCJyZWZlcnJhbFZpZXciOiJNeUZpbGVzTGlua0NvcHkifX0&e=Zle5Ts)
