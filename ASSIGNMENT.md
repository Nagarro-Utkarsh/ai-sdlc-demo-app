# Invoice approval assignment write-up

## Scope and result
This repository implements the role-based invoice approval feature described in the assignment. The approval service enforces the single-approver limits and the two-step approval flow for high-value invoices, while rejecting invalid approval attempts without mutating invoice state.

The main files involved are:
- `backend/src/InvoiceApp.Domain/ApprovalService.cs` — approval rules and lifecycle transitions
- `backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs` — unit tests for limits, approval order, and same-user rejection cases
- `e2e/tests/approval-rejection.spec.ts` — end-to-end coverage of a rejected over-limit approval
- `AGENTS.md` — contributor instructions and required workflow expectations
- `openspec/changes/invoice-approval-limits/` — spec, design, and task breakdown
- `AI_INTERACTION_LOG.jsonl` — structured AI interaction records

## Specification and task evidence
The assignment requirement is captured in the OpenSpec change package:
- `openspec/changes/invoice-approval-limits/specs/invoice-approval/spec.md`
- `openspec/changes/invoice-approval-limits/design.md`
- `openspec/changes/invoice-approval-limits/tasks.md`

The implemented behavior matches the assignment wording:
- Manager single-approval limit: 50,000 inclusive
- Director single-approval limit: 500,000 inclusive
- Amounts above 500,000 require a Manager approval followed by a different Director approval
- The same user cannot appear as both approvers on the same invoice
- A rejected attempt returns a clear reason and leaves the invoice unchanged

The existing bulk-approval use case now explicitly preserves these per-invoice role limits and second-approval checks. The partial-approval use case preserves the current role policy and records as an open question whether a future partial-approval decision should use the original or approved amount when applying limits.

## Verification commands and evidence

1. Unit tests
```bash
cd ai-sdlc-demo-app
~/.dotnet/dotnet test backend/InvoiceApp.sln --nologo --verbosity minimal
```
Result:
- Passed: 19
- Failed: 0
- Skipped: 0

2. End-to-end test
```bash
cd ai-sdlc-demo-app/e2e
PATH=/Users/utkarsh.mahajan/.dotnet:$PATH npm test
```
Result:
- 1 test run
- 1 passed
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

5. Mutation coverage
```bash
cd ai-sdlc-demo-app
DOTNET_ROOT=/Users/utkarsh.mahajan/.dotnet \
PATH=/Users/utkarsh.mahajan/.dotnet:/Users/utkarsh.mahajan/.dotnet/tools:$PATH \
dotnet-stryker --solution backend/InvoiceApp.sln \
  --project backend/src/InvoiceApp.Domain/InvoiceApp.Domain.csproj \
  --test-project backend/tests/InvoiceApp.Tests/InvoiceApp.Tests.csproj \
  --mutate '**/ApprovalService.cs'
```
Stryker 4.13.0 was run with the same command and mutation filter on the original `main` baseline and the completed feature branch:

| Run | Tests | Killed | Survived | No coverage | Mutation score |
|---|---:|---:|---:|---:|---:|
| Baseline (`66cb3f9`) | 7 | 12 | 8 | 0 | 60.00% |
| Feature branch | 19 | 52 | 0 | 5 | 91.23% |

- Timeout: 0
All 52 tested mutants were killed; five mutants had no test coverage. The generated report was removed during workspace cleanup; the scores and mutant counts above were recorded from the completed run.

## PR review findings and verdicts
Feature branch: [feat/invoice-approval-limits](https://github.com/Nagarro-Utkarsh/ai-sdlc-demo-app/tree/feat/invoice-approval-limits). Submission PR: [Nagarro-Utkarsh/ai-sdlc-demo-app#2](https://github.com/Nagarro-Utkarsh/ai-sdlc-demo-app/pull/2), open against `main`, created by `Nagarro-Utkarsh` from the branch in the `Nagarro-Utkarsh/ai-sdlc-demo-app` repository. The superseded PR #1 from the personal fork is closed. The review followed `.github/prompts/review-approval-change.prompt.md` against the PR code diff and compared it with `AGENTS.md`, the OpenSpec acceptance criteria, and the assignment. Scope included approval transitions, role guards, UI progress hint, unit tests, Playwright rejection flow, Copilot customizations, and interaction logger. The code tree was unchanged by the subsequent commit-message-only history rewrite. The PR description records the finding and verdict.

| # | Finding | Verdict |
|---|---------|--------|
| 1 | `.github/hooks/interaction_log.py`: `record_for` ignored the documented `event_type` field for manual assistant-response entries, causing the CLI-accepted event to be recorded as unknown without its response. | Fixed by accepting `event_type` in the record builder and adding a regression test; all four logger tests pass. |
| — | No other actionable correctness issue found in the reviewed PR scope. | No change required. |

## AI log
The assignment log is maintained at:
- `AI_INTERACTION_LOG.jsonl`

The repository also includes the log tooling and validation guidance in:
- `docs/AI_INTERACTION_LOG.md`
- `.github/hooks/interaction_log.py`
- `.github/hooks/test_interaction_log.py`

The logger validation command ran successfully:
```bash
cd ai-sdlc-demo-app
python3 -m unittest discover -s .github/hooks -p 'test_*.py'
```
Result: 4 tests ran, all passed.
The log includes manually structured summaries and a clearly labeled retrospective summary; it is not a verbatim or complete transcript. The logger was tested with its unit suite and a sample Copilot-format hook payload, but live VS Code hook activation was not verified. A session-context summary was available for this active Copilot session; a full VS Code transcript export was not available in this environment.

## Demo video
Video link: pending upload in this environment. No valid public upload or verified link was produced here, so this section intentionally avoids inventing a file or URL.

## Notes
The code and local verification evidence are present, and the feature branch and PR are published in the intended repository under the required GitHub account. The PR body includes the review finding and verdict. Uploading the required demo video remains outstanding; this environment did not produce or verify a video link.
