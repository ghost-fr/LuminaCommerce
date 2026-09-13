# Phase 6 — Cash Drawer Reconciliation

## Scope decision: cash-drawer reconciliation, NOT full accounts-receivable

The roadmap phase name is "Cash & receivables," but its own listed UI deliverable
is "Register open/close UI, receipts screen" — no invoice-payment/AR screen
mentioned. I read that as: this phase means reconciling the physical cash
drawer against register open/close, not tracking unpaid B2B invoice balances.
Built accordingly. If full accounts-receivable (due dates, partial payments,
aging) turns out to be needed, that's worth designing as its own deliberate
contract — flagged, not guessed at here.

## What's implemented and tested

- **`RegisterSession`** (Domain.Cash) — one open-to-close cash-handling cycle for
  a `Register`. Separate aggregate from `Register` itself: `Register` is the
  long-lived terminal, `RegisterSession` is one shift's cash accountability
  record. Computes `Discrepancy` on close from a caller-supplied
  `ExpectedClosingCash` (no I/O inside the aggregate, same discipline as `Sale`).
- **`CashMovement`** (Domain.Cash) — append-only, same pattern as
  `StockMovement`. Four types: `CashSaleTender`, `CashRefundTender` (unused —
  see below), `CashIn`, `CashOut`. Sign and reason-requirement enforced at
  construction (e.g. `CashOut` must be negative, `CashIn`/`CashOut` require a
  reason).
- **`RegisterSessionService`** — implements `IRegisterSessionService` directly
  (not via a separate adapter). Opens/closes `Register` and `RegisterSession`
  together, atomically, in the same unit of work — they can never drift apart.
  Resolves the acting user from `IAuthService.CurrentSession`, not from UI input.
- **`CashDrawerService`** — the `PosSaleService` integration point. Records the
  cash portion of a completed sale's tenders against the register's open
  session, in the same atomic commit as the `Sale` and stock/fiscal records
  (extending the pattern from Phase 4/5).
- Closed a real gap found while building this: **`IRegisterRepository` had no
  `UpdateAsync`** — nothing before Phase 6 ever called `Register.Open()`/`Close()`
  from Application code, so nothing needed to persist that mutation. Added.
- Tests: 15 domain tests (`CashMovement` validation rules, `RegisterSession`
  discrepancy math), 12 application tests (`RegisterSessionService` full
  open/close/cash-in-out flows), plus 4 new `PosSaleServiceTests` cases covering
  the cash-drawer integration specifically.

## Important finding, worth surfacing to whoever's building the tender UI

**`TenderType.MixedCashCard` as a single tender line is now rejected outright**
by `PosSaleService.CompleteSaleAsync` (`RejectionCode.PaymentValidationFailed`).
Reasoning: accurate cash-drawer reconciliation needs to know exactly how much
of a sale's tenders was physical cash. A single "mixed" line gives no way to
recover that split — guessing (50/50, or treating the whole amount as cash, or
ignoring it) would silently corrupt every subsequent register-close discrepancy
calculation. The fix is architecturally already supported:
`CompleteSaleRequest.Tenders` is a list, so a genuinely mixed payment should be
sent as **two separate `TenderLine`s** (one `Cash`, one `Card`), which nets out
correctly with no ambiguity.

**This may already conflict with UI work in progress** — an earlier POS mockup
showed a single "Mixto" payment button, which would need to become "collect two
amounts, cash and card" rather than producing one `MixedCashCard` line. Worth
confirming with whoever owns that screen before it's built further in the old
shape.

## Flagged decisions and gaps

1. **`CashRefundTender` exists but nothing generates one.** No refund flow has
   been built in any phase yet (POS sale, quote/invoice — none of them have a
   "give money back" path). The type exists now so the eventual refund feature
   doesn't need a `CashMovement` schema change, same forward-compatibility
   reasoning as `Invoice.SourceQuoteId` in Phase 5.

2. **`RegisterSessionRepository.UpdateAsync` is effectively a no-op comment.**
   Unlike `Cart`/`Sale`/`Quote` (which use the JSON-projection pattern and
   genuinely need an explicit re-serialize-and-save step), `RegisterSession` has
   no encapsulated collection, so EF's change tracker already sees mutations on
   the instance `FindByIdAsync` returned. The explicit `Update()` call is kept
   anyway for consistency with every other repository in the codebase, not
   because this aggregate specifically needs it. Documented in the repository
   itself so it doesn't look like a mistake on review.

3. **No partial-index support for "the open session per register" query.**
   `FindOpenByRegisterIdAsync` filters `Status = Open` at the SQL level (a
   normal composite index on `(RegisterId, Status)`, not a partial/filtered
   index) — fine at this scale, worth knowing if register-session volume ever
   gets large enough for it to matter.

4. **Same "declared vs actual" trust model as everywhere else in this phase:**
   `DeclaredClosingCash` is exactly what the cashier types in — there's no
   independent verification (e.g. a coin-counting device) built or assumed. The
   `Discrepancy` field exists precisely because this number is operator-reported,
   not measured.
