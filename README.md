# Invoice Approval Demo App

A small invoice approval app used for live AI-in-SDLC demos. It is deliberately simple: a .NET 8 API, a plain web UI, and an in-memory store, so it runs with one command and no database.

## Run it

```
dotnet run --project backend/src/InvoiceApp.Api
```

Open http://localhost:5000 and sign in:

| User | Password | Role |
| --- | --- | --- |
| `demo.manager` | `ChangeMe123!` | Manager |
| `demo.director` | `ChangeMe123!` | Director |
| `demo.admin` | `ChangeMe123!` | Admin |

These are demo accounts for a local, in-memory app. They are not real credentials.

## What it does

1. Create an invoice (it starts as a draft).
2. Submit it for approval.
3. A Manager or Director approves it within their limit, or rejects it with a reason.

Managers may singly approve invoices up to 50,000; Directors may singly approve up to 500,000. Invoices above 500,000 require a Manager's approval followed by a different Director's approval.
Invoices move through Draft, Pending Approval, then Approved or Rejected.

For an invoice above 500,000, the first Manager approval leaves it pending and records the first approver. A different Director must provide the second approval to complete it. Rejected approval attempts return a reason without changing invoice state.

## Tests

```
dotnet test backend/InvoiceApp.sln
```

Run the browser end-to-end test with:

```
cd e2e
npm ci
npx playwright install chromium
npm test
```

## Project layout

```
backend/
  src/InvoiceApp.Domain   business rules
  src/InvoiceApp.Api      controllers, sign-in, web UI (wwwroot)
  tests/InvoiceApp.Tests  xUnit tests
docs/use-cases            short feature briefs
AGENTS.md                 conventions for AI coding agents
.github/                  GitHub Copilot instructions, agents, prompts, skills and hooks
```

## GitHub Copilot workflow

Open this repository as the VS Code workspace and select the Copilot agent harness. Project instructions and reusable Copilot agents, prompts, skills, and hooks live under `.github/`. See [docs/AI_INTERACTION_LOG.md](docs/AI_INTERACTION_LOG.md) for logging behavior and the limits of automatic response capture.

## Use cases

- [UC-1 Bulk invoice approval](docs/use-cases/UC-1-bulk-approval.md)
- [UC-2 Partial approval](docs/use-cases/UC-2-partial-approval.md)
