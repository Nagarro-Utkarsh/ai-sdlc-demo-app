# Invoice approval assignment write-up

## Scope and result
This repository implements role-based full and partial invoice approvals. The approval service enforces the single-approver limits and the two-step flow for high approved amounts, while rejecting invalid approval attempts without mutating invoice state.

The main files involved are:
- `backend/src/InvoiceApp.Domain/ApprovalService.cs` — approval rules and lifecycle transitions
- `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs` — unit tests for limits, approval order, and same-user rejection cases
- `e2e/tests/approval-rejection.spec.ts` — end-to-end coverage of a rejected over-limit approval
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

The existing bulk-approval use case preserves these per-invoice role limits and second-approval checks.

## Verification commands and evidence

Verification includes the partial-approval implementation. The backend unit suite and Playwright end-to-end suite both pass.

1. Unit tests
```bash
cd ai-sdlc-demo-app
~/.dotnet/dotnet test backend/InvoiceApp.sln --nologo --verbosity minimal
```
Result:
- Passed: 27
- Failed: 0
- Skipped: 0

2. End-to-end test
```bash
cd ai-sdlc-demo-app/e2e
PATH=/Users/utkarsh.mahajan/.dotnet:$PATH npm test
```
Result:
- 2 tests run
- 2 passed
- 0 failed
The E2E server uses port 5055 because macOS AirTunes already occupies port 5000 on this machine.

3. API authorization smoke test
Using the local API at `http://127.0.0.1:5058`, demo-user logins succeeded and the following requests were observed:
- Manager `POST /api/approvals/INV-1001/approve`: HTTP 200; invoice became `Approved`.
- Admin `POST /api/approvals/INV-1002/approve`: HTTP 403.
- Admin `POST /api/approvals/INV-1002/reject` with a reason: HTTP 200.

4. API build
```bash
cd ai-sdlc-demo-app
~/.dotnet/dotnet build backend/src/InvoiceApp.Api/InvoiceApp.Api.csproj --nologo --verbosity minimal
```
Result: build succeeded with 0 warnings and 0 errors.

## AI log
The assignment log is maintained at:
- `AI_INTERACTION_LOG.jsonl`

Prompt and response capture is covered by an interaction test that runs the hook commands against a temporary repository and verifies the appended JSONL record. The log tooling is in:
- `.github/hooks/interaction_log.py`
- `.github/hooks/test_interaction_log.py`

The logger validation command ran successfully:
```bash
cd ai-sdlc-demo-app
python3 -m unittest discover -s .github/hooks -p 'test_*.py'
```
Result: 11 tests ran, all passed.
The `userPromptSubmitted` hook writes submitted text into each prompt record's `prompt` field after redaction. The `agentStop` hook then reads the completed exchange from the provided transcript path and fills the matching `response` field. Unit tests exercise the prompt and stop events together using transcript-shaped input; live VS Code hook activation remains unverified.

## Demo video
Video link: pending upload in this environment. No valid public upload or verified link was produced here, so this section intentionally avoids inventing a file or URL.
