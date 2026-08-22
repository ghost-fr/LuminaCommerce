# Lumina Commerce Platform — Production Implementation Plan
## v1.0 Blueprint → Buildable Repo (Claude / Grok split)

This turns `LUMINA_COMMERCE_ARCHITECTURE_BLUEPRINT.md` into something two AI collaborators
and one human can actually execute against in a shared GitHub repo without stepping on
each other. It assumes:

- **Stack:** .NET 8, C#, Avalonia (POS/back-office UI), EF Core + SQLite (single-store now,
  Postgres/SQL Server later per the blueprint's infra layer).
- **Agents:** Claude owns domain/application/infrastructure/fiscal logic. Grok owns
  Avalonia UI (XAML, styles, view models' *presentation* concerns, UX polish).
  You (Ahmad) own product decisions, merges to `main`, and anything crossing the boundary.
- **Repo:** one GitHub repo, trunk-based with short-lived feature branches, not a monorepo
  split into two disconnected apps.

If any of those assumptions are wrong — e.g. you want Grok on the backend too, or a
different UI framework — say so and I'll adjust the split below.

---

## 1. Why the v1.0 blueprint isn't production-shape yet

The blueprint is architecturally sound (clean layering, VeriFactu as a first-class port,
outbox pattern, append-only stock ledger) but it's a *design*, not a build plan. Four gaps
stand between it and something shippable:

1. **No contract-first boundary.** UI and backend can't be built in parallel without
   frozen interfaces (commands, DTOs, ViewModels) to build against. Right now the doc
   describes both sides conceptually but doesn't pin the seam.
2. **No repo/CI shape.** Solution layout, branch policy, build pipeline, and test gates
   aren't defined — needed before two agents touch the same repo.
3. **No environment/config story.** Blueprint mentions "encrypted configuration" but not
   how secrets (AEAT certs, DB connection strings) are handled per environment
   (dev/staging/prod) or kept out of git.
4. **No test strategy.** Fiscal-critical code (hash chaining, VeriFactu records) needs unit
   tests with fixed vectors before it's trustworthy; this isn't in the v1.0 doc at all.

The rest of this plan fills those four gaps and re-sequences Section 10's roadmap around
a two-agent workflow.

---

## 2. Repo structure

```text
SupermarketPOS/
├── src/
│   ├── Lumina.Domain/                 # Aggregates, value objects, domain events — CLAUDE
│   ├── Lumina.Application/            # Commands, handlers, services, ports — CLAUDE
│   ├── Lumina.Infrastructure/         # EF Core, repos, VeriFactu client, outbox — CLAUDE
│   ├── Lumina.Fiscal/                 # Hash chain, QR, AEAT XML/SOAP — CLAUDE
│   ├── Lumina.Contracts/              # Shared DTOs/interfaces UI binds to — SHARED, FROZEN
│   ├── Lumina.Pos.UI/                 # Avalonia POS client (views, styles, XAML) — GROK
│   ├── Lumina.BackOffice.UI/          # Avalonia back-office app (views, styles) — GROK
│   └── Lumina.Shared.Controls/        # Reusable Avalonia components/theme — GROK
├── tests/
│   ├── Lumina.Domain.Tests/
│   ├── Lumina.Application.Tests/
│   ├── Lumina.Fiscal.Tests/           # Hash-chain fixed vectors, QR payload golden files
│   └── Lumina.Pos.UI.Tests/           # ViewModel logic only, not visual
├── .github/workflows/ci.yml
├── docs/
│   ├── LUMINA_COMMERCE_ARCHITECTURE_BLUEPRINT.md
│   ├── CONTRACTS.md                   # Frozen interface reference (see §4)
│   └── ADRs/                          # One markdown file per architectural decision
├── config/
│   ├── appsettings.Development.json   # No secrets — placeholders only
│   └── appsettings.schema.json
└── .env.example
```

**Rule:** `Lumina.Contracts` is the only project both sides reference. Neither agent edits
the other's folders without an explicit PR review from you. Grok never touches
`Domain`/`Application`/`Infrastructure`/`Fiscal`. Claude never touches `.UI` XAML/styling —
at most proposes ViewModel *interfaces* that live in `Contracts`.

---

## 3. Branching & workflow

- `main` — always buildable, protected, requires PR + passing CI.
- `feat/backend/<slice>` — Claude's branches (e.g. `feat/backend/pos-sale-command`).
- `feat/ui/<slice>` — Grok's branches (e.g. `feat/ui/pos-cart-screen`).
- No agent pushes directly to `main`. You merge, or approve auto-merge once CI is green.
- **Slice, don't layer, the work.** Each branch should deliver one vertical capability
  (e.g. "POS sale happy path") touching only the files needed for that slice — not
  "all of Domain" in one PR. Keeps both agents' diffs reviewable and reduces merge
  conflicts on shared files like `Contracts`.
- Any change to `Lumina.Contracts` needs its own small PR, reviewed and merged *before*
  the UI or backend branches that depend on it are opened — this is the seam, treat it
  like an API contract, not an implementation detail.

---

## 4. Contract-first seam (`Lumina.Contracts`)

This is the piece the v1.0 blueprint doesn't specify and the piece that makes parallel
Claude/Grok work possible. Before either side builds a feature, agree the shape here:

```csharp
// Lumina.Contracts/Pos/ICartViewModel.cs
public interface ICartLine
{
    string ProductName { get; }
    string Barcode { get; }
    decimal UnitPrice { get; }
    decimal Quantity { get; }
    decimal VatRate { get; }
    decimal LineTotal { get; }
}

// Lumina.Contracts/Pos/PosCommands.cs — what the UI can invoke
public record AddLineRequest(string Barcode, decimal Quantity);
public record CompleteSaleRequest(Guid RegisterId, Guid? CustomerId, IReadOnlyList<TenderLine> Tenders);
public record CompleteSaleResult(Guid SaleId, string TicketNumber, string QrPayload, bool VeriFactuSubmitted);
```

- Claude implements the command handlers behind these; Grok binds Avalonia views to
  view-model classes that call them (via DI-injected application services, never
  directly touching `Infrastructure`).
- Every new command/query gets added to `docs/CONTRACTS.md` with a one-line description
  before either side codes against it — a lightweight API-first discipline without needing
  full OpenAPI tooling for an in-process desktop app.

---

## 5. CI gates (minimum viable)

```yaml
# .github/workflows/ci.yml (shape, not final)
on: [pull_request]
jobs:
  build-and-test:
    steps:
      - dotnet build (fails on warnings in Domain/Application/Fiscal)
      - dotnet test tests/Lumina.Domain.Tests
      - dotnet test tests/Lumina.Application.Tests
      - dotnet test tests/Lumina.Fiscal.Tests      # hash-chain + QR golden vectors — non-negotiable
      - dotnet format --verify-no-changes
```

UI PRs (Grok's branches) can skip fiscal tests but must still build and pass
`Lumina.Pos.UI.Tests` (ViewModel logic, no fiscal math). Backend PRs never merge if
`Lumina.Fiscal.Tests` fails — this is the one place a bug is a legal problem, not just
a bug.

---

## 6. Environment & secrets

- `appsettings.*.json` in git contains **no real secrets** — only placeholders and a
  `appsettings.schema.json` for validation.
- Real values (AEAT client certificate, DB connection string, encryption keys) go in
  `.env` (git-ignored) locally, and in GitHub Actions/environment secrets for CI/CD.
- Three environments minimum: `Development` (local SQLite, VeriFactu sandbox/mocked),
  `Staging` (AEAT pre-production endpoint if available, or a stub), `Production`.
- AEAT certificate handling is Infrastructure/Fiscal — Claude's lane — since it's a
  compliance-sensitive credential, not a UI concern.

---

## 7. Re-sequenced roadmap (two-agent version of blueprint §10)

| Phase | Backend (Claude) | UI (Grok) | Gate before next phase |
|---|---|---|---|
| 0 | Repo scaffold, CI, `Contracts` skeleton | Solution refs, empty Avalonia shells | Both build green in CI |
| 1 | Foundation: tenants/stores/users/roles, EF Core migrations | Login/session shell, capability-based nav skeleton | Auth round-trips end to end |
| 2 | Product/tax master data, pricing engine | Product list/search screens, price display | Can browse a seeded catalogue in UI |
| 3 | **POS sale command + VeriFactu hash chain (fiscal-critical)** | POS cart screen, tender UI, ticket render | Fixed-vector hash tests pass; a real sale round-trips with a valid QR |
| 4 | Stock ledger | Stock adjustment/count screens | Sale correctly decrements stock |
| 5 | Commercial documents state machine | Quote/invoice/credit-note screens | Full quote→invoice flow in UI |
| 6 | Cash & receivables | Register open/close UI, receipts screen | Register close reconciles to ledger |
| 7 | Reporting read models | Dashboards/report screens | Daily report matches raw sale data |
| 8 | Device ports (printer/scanner/scale) | Device status indicators | Real or emulated device round-trip |
| 9 | AEAT integration hardening, security review | UI polish pass, accessibility | VeriFactu declaration checklist complete |

Phase 3 is the one to protect most carefully — it's where fiscal correctness and UI
usability both matter and where a Claude/Grok handoff bug would be costliest. Recommend
you personally review that PR pair (backend + UI) together rather than merging
independently.

---

## 8. What to do next

1. Confirm or correct the assumptions in the intro (stack, agent split, repo model).
2. I can scaffold Phase 0 now — solution structure, `Lumina.Contracts` skeleton, CI
   workflow file — if you want to hand Grok something concrete to build the UI shell
   against immediately.
3. If you'd rather I draft `docs/CONTRACTS.md` with the first real seam (POS sale) fully
   specified before any code, I can do that instead — usually the faster path to
   unblocking both agents in parallel.

Tell me which of #2/#3 to start with, or both.
