# Lumina.Contracts — Seam Reference

This document is the source of truth for the interface between backend (Claude) and UI
(Grok) work. Code in `src/Lumina.Contracts` is the *implementation* of what's described
here; if the two ever disagree, this document wins until both sides agree on an update.

**Change process:** propose a change here first (small PR touching only this file +
`Lumina.Contracts`), get it merged, *then* open backend/UI branches against the new
shape. Don't change contract code and consuming code in the same PR unless you're the
one merging both sides.

---

## Status

| Contract | Status | Owner of current spec |
|---|---|---|
| Auth/session (`IAuthService`, `IUserSession`) | Frozen v1 | This doc, §0 |
| Cart (`ICartLine`, `ICartSummary`) | Frozen v1 | This doc, §1 |
| POS sale (`IPosSaleService`, commands) | Frozen v1 — **backend implemented** | This doc, §2 |
| Product catalogue (`IProductCatalogueService`) | Frozen v1 — **backend implemented** | This doc, §3 |
| Commercial documents (`IQuoteService`, `IInvoiceService`) | Frozen v1 — **backend implemented (Quote+Invoice; CreditNote not yet built)** | This doc, §4 |
| Cash / register sessions (`IRegisterSessionService`) | Frozen v1 — **backend implemented** | This doc, §5 |
| Reporting (`IReportingService`) | Frozen v1 — **backend implemented** | This doc, §6 |
| Devices (`IReceiptPrinter`, `IScaleService`) | Frozen v1 — **backend implemented, EMULATED only** | This doc, §7 |

---

## 0. Auth / session contract

`Lumina.Contracts.Auth.IAuthService` / `IUserSession` — the login screen and
capability-gated nav shell build against this. This is Phase 1, ready now.

**UI (Grok) may assume:**
- `IAuthService.CurrentSession` is `null` until login succeeds — use that as the
  single source of truth for "show login screen" vs. "show main nav," don't track a
  separate `isLoggedIn` flag in the UI.
- `IUserSession.HasCapability(...)` is a pure, synchronous, side-effect-free check
  against an already-loaded set — safe to call from XAML value converters/bindings to
  show/hide menu items and buttons. It never hits the network or DB.
- `LoginResult.Status` has four values (`Success`, `InvalidCredentials`, `UserInactive`,
  `StoreNotAssigned`) — UI should show a distinct, friendly message per status rather
  than one generic "login failed," since the fix differs (retry password vs. contact
  admin vs. store assignment issue).
- Single-store tenants (the common case) never need a store picker — omit
  `RequestedStoreId` in `LoginRequest` and the backend resolves the default store
  automatically.

**Backend (Claude) must guarantee:**
- Password hashes are never exposed anywhere in `IUserSession` or `LoginResult` — UI
  gets capabilities and identity only, never credential material.
- `Capabilities` strings are stable, documented constants (`Lumina.Domain.Identity.
  Capabilities`) — UI can hardcode capability-string checks in XAML bindings without
  fear of silent renames breaking nav visibility.

---

## 1. Cart contract

`Lumina.Contracts.Pos.ICartLine` / `ICartSummary` — read-only projections the POS UI
binds to directly (e.g. wrap in an `ObservableCartLine` ViewModel that implements
`ICartLine` for Avalonia binding, or adapt via a thin wrapper).

**UI (Grok) may assume:**
- `LineTotal` is already VAT-inclusive-or-exclusive per store config — don't recompute
  tax in the UI layer; display what's given.
- `AppliedPromotionCode` is `null` when no promotion applied — safe to bind directly to
  a "promo applied" badge visibility.
- Collections (`Lines`) are already in display order; don't re-sort.

**Backend (Claude) must guarantee:**
- Every `ICartSummary` returned reflects a fully re-validated cart (prices/promotions
  re-resolved against current `PricingService` state, not cached from add-time).
- `Subtotal + VatTotal == Total` to the cent, always — UI will not defensively recompute
  this and a mismatch is a backend bug, not a UI bug.

---

## 2. POS sale contract

`Lumina.Contracts.Pos.IPosSaleService` — the single entry point the POS UI uses to
mutate a cart and complete a sale. This is Phase 3, the fiscal-critical milestone.

### 2.1 Flow the UI drives

```
AddLineAsync (barcode scan)       →  ICartSummary   (re-render cart)
RemoveLineAsync (line edit)       →  ICartSummary   (re-render cart)
CompleteSaleAsync (tender submit) →  CompleteSaleResult
```

### 2.2 `CompleteSaleAsync` contract — read carefully

This is the one call in the whole system where UI defensive coding matters most,
because a mishandled response can mean printing a ticket for a sale that didn't
legally happen, or failing to print one that did.

- **`CompleteSaleStatus.Success`** — Sale committed, VeriFactu record generated and
  (if VERI*FACTU mode is on) already submitted or queued synchronously enough to
  treat as done. UI prints ticket using `TicketNumber` + renders `QrPayload` as the
  QR code per blueprint §8.1 sizing/placement rules. UI clears the cart.

- **`CompleteSaleStatus.SuccessPendingSubmission`** — Sale committed locally
  (hash-chained record exists), AEAT submission is async/retrying. **UI treats this
  identically to `Success`** for ticket printing — the sale happened, the ticket is
  legally valid, submission status is a background concern surfaced elsewhere (e.g. a
  register-level "pending submissions: N" indicator), not blocking the current ticket.

- **`CompleteSaleStatus.Rejected`** — Nothing was committed. UI must **not** print
  anything, must **not** clear the cart, and must surface `RejectionReason` to the
  operator. Common causes: stock unavailable, payment validation failed, register not
  open. UI should let the operator retry or adjust tenders without re-scanning the cart.

- **UI must not synthesize a `QrPayload` or `TicketNumber` on its own under any
  circumstance** — these only ever come from a `Success`/`SuccessPendingSubmission`
  result. If the backend call throws or times out, treat as unknown/failed, never as
  success — show a retry/reconcile path, never a printed ticket.

### 2.3 What backend guarantees before returning `Success*`

- The `Sale` aggregate is persisted and immutable.
- Stock movement events are emitted.
- A VeriFactu invoicing record exists, SHA-256 hash-chained to the previous record in
  the configured SIF boundary (blueprint §8.4).
- `QrPayload` is a complete, spec-compliant payload (blueprint §8.1) — UI only needs to
  render it as a QR code at the mandated size/contrast/quiet-zone, not construct it.
- An outbox entry exists for AEAT submission (if VERI*FACTU mode active).

### 2.4 Resolved decisions (was: open questions)

- **`RejectionCode`** — added as a typed enum alongside free-text `RejectionReason`.
  UI switches on `RejectionCode` for the primary message/styling; `RejectionReason`
  is secondary detail (e.g. an expandable "details" line), not the primary copy.
- **`IdempotencyKey`** — added to `CompleteSaleRequest` (client-generated `Guid`).
  UI generates one fresh key per *submission attempt*, reuses the same key on retry
  of that same attempt (timeout/dropped connection), and generates a new key only
  for a genuinely new sale attempt. Backend uses it to no-op duplicate submissions
  rather than risk a double sale — this is what makes retry-after-timeout safe.

### 2.5 `RejectionCode.FiscalChainError` — now a real, reachable outcome

As part of a correctness-hardening pass, the backend implemented the AEAT-required
chain-integrity pre-check (OM HAC/1177/2024 art. 7.i) — before generating any fiscal
record beyond the first in a chain, it verifies the previous record's own chain link
is intact and its timestamp is consistent. `RejectionCode.FiscalChainError` has
existed in this contract since Phase 3 but had no code path that could ever trigger
it until now. UI should have a real, distinct message for this code (not lump it
into a generic error) — it signals the fiscal chain itself needs investigation, not
an ordinary retryable business rejection like `StockUnavailable`. In practice this
should be rare (it only fires on genuine data corruption or a clock/ordering
problem), but "rare" is different from "impossible," and the UI should treat it
seriously when it appears — e.g. surfacing it prominently rather than auto-retrying.

---

## 3. Product catalogue contract

`Lumina.Contracts.Catalogue.IProductCatalogueService` — product browsing/search and
barcode lookup. Fully implemented and tested.

### 3.1 `IProductSummary` — what UI can assume

- `CurrentPrice` already reflects any active promotion — never recompute or apply a
  discount client-side.
- `OriginalPrice` is `null` when no promotion is active; non-null (and greater than
  `CurrentPrice`) when one is — safe to bind directly to a "was X, now Y" strike-through
  display without an extra check beyond the null.
- `HasActivePromotion` exists as a convenience even though it's derivable from
  `OriginalPrice != null` — use whichever reads more naturally in the binding.

### 3.2 `FindByBarcodeAsync` — POS scan flow

Returns `null` on no match **or** an inactive product — UI shows a single "product not
found" state for both cases rather than distinguishing "doesn't exist" from
"discontinued." If that distinction turns out to matter for the cashier workflow,
that's a contract change to propose here first, not a UI-side workaround.

### 3.3 Tenant-scoping wiring — resolved

`ProductCatalogueService.SearchAsync`/`FindByBarcodeAsync` are fully implemented
against `ICurrentTenantProvider`, same pattern as `AuthService`. `PosSaleService`
calls the public `FindByBarcodeAsync` directly. No known gaps remain in this
contract.

---

## 4. Commercial documents contract (Quote → Invoice)

`Lumina.Contracts.Commercial.IQuoteService` / `IInvoiceService` — quote building,
the quote lifecycle, conversion to a real fiscal invoice, and invoice cancellation.
Backend implemented and tested for Quote and Invoice. **CreditNote (factura
rectificativa) is NOT built yet** — deferred as its own follow-up; see
`docs/PHASE5_NOTES.md`.

### 4.1 Quote lifecycle — what UI can assume

- State machine: `Draft -(Send)-> Sent -(Accept/Reject)-> Accepted/Rejected`.
  Only a **Draft** quote accepts `AddLineAsync`/`RemoveLineAsync` — calling either
  on a quote in any other status throws. UI should disable line-editing controls
  once a quote leaves Draft, not treat the exception as a normal rejection path
  (unlike `CompleteSaleAsync`, this isn't a `Result` with a rejection code — it's
  a genuine programming error if the UI lets this happen).
- `CreateQuoteRequest` takes `StoreId` explicitly (see the interface's XML doc
  for why — no prior aggregate to inherit it from at creation time). Populate
  from `IUserSession.StoreId`.
- A quote requires a real `CustomerId` — unlike a POS `Sale`, quotes are never
  anonymous.

### 4.2 `ConvertToInvoiceAsync` — the fiscal-critical call, same discipline as `CompleteSaleAsync`

- Only callable when the quote is **Accepted** — any other status returns
  `ConvertToInvoiceStatus.Rejected` with a reason, nothing is created.
- Uses the **same idempotency-key discipline** as `CompleteSaleAsync`
  (§2.2): one key per submission attempt, reused on retry, never invent
  `InvoiceNumber`/`QrPayload` client-side.
- **Known gap, same as `CompleteSaleAsync`:** a retried call with the same
  idempotency key currently returns `QrPayload: null` — the QR isn't stored on
  the `Invoice` aggregate. Same unresolved item as `CONTRACTS.md`'s POS-sale
  section, not re-solved here.

### 4.3 `IInvoiceService.CancelAsync` — cancellation is a real fiscal event

- Requires a non-empty `reason` — UI must require the operator to type one, not
  send a placeholder. (Note: the reason is validated and stored at the Lumina
  level for audit purposes but is **not** part of the fiscal hash itself — AEAT's
  confirmed cancellation-record field set has no free-text field.)
- Only callable on an **Issued** invoice — cancelling an already-cancelled or
  rectified invoice returns `Rejected`.
- This generates a real, separate VeriFactu "anulación" record — treat it with
  the same seriousness as completing a sale, not as a quick local undo.

### 4.4 Not yet built — flagged for whoever picks these up next

- **CreditNote (factura rectificativa)** — the domain/application/infra pattern
  established here (Quote→Invoice, Invoice→Cancel) is the template to extend;
  see `docs/PHASE5_NOTES.md` for specifics on why it was deliberately deferred
  rather than rushed.
- **Direct invoice issuance without a prior quote** — `Invoice.SourceQuoteId` is
  nullable specifically so this can be added later without a data-model change,
  but no service method creates one that way today.

---

## 5. Cash / register sessions contract

`Lumina.Contracts.Cash.IRegisterSessionService` — register open/close and manual
cash in/out. Fully implemented and tested. This is what makes `PosSaleService`'s
long-standing `RegisterNotOpen` rejection code mean something real: opening a
register now actually starts cash tracking, not just flips a boolean.

### 5.1 What UI can assume

- The acting user comes from the current `IAuthService.CurrentSession` — **not**
  passed in `OpenRegisterRequest`/`CloseRegisterRequest`. If nobody is logged in,
  these throw (a programming error, not a normal rejection — UI should never be
  able to reach this screen unauthenticated).
- `CloseRegisterResult` on success always includes both `ExpectedClosingCash` and
  `Discrepancy` — surface both to the operator, not just a pass/fail message.
  `Discrepancy = DeclaredClosingCash - ExpectedClosingCash`: positive means more
  cash was counted than expected, negative means less.
- `RecordCashMovementAsync` requires a non-empty `Reason` — same discipline as
  `IInvoiceService.CancelAsync` (§4.3).
- A register must be **Open** (via `OpenAsync`) before `IPosSaleService.
  CompleteSaleAsync` will accept sales against it (unchanged rejection code,
  now backed by real state).

### 5.2 Interaction with POS sale — important for the cart/tender screen

- **`TenderType.MixedCashCard` as a single tender line is now rejected outright**
  (`RejectionCode.PaymentValidationFailed`) — the backend has no way to know how
  much of a "mixed" line was physical cash, and guessing would silently corrupt
  register-close reconciliation. A genuinely mixed payment must be sent as
  **two separate `TenderLine`s** — one `Cash`, one `Card` — which
  `CompleteSaleRequest.Tenders` already supports as a list. **If the tender
  screen currently offers a single "Mixto" button producing one `MixedCashCard`
  line, that needs to change to collecting two separate amounts.**
- Only the `Cash`-type tender amount affects the drawer — `Card` tenders never
  generate a cash movement, by design.

### 5.3 Not yet built

- **Receivables / accounts-receivable** (tracking unpaid B2B invoice balances,
  due dates, partial payments) is explicitly **out of scope** for this contract
  — the roadmap's UI description for this phase ("register open/close, receipts
  screen") doesn't mention an invoice-payment screen, so this was interpreted as
  cash-drawer reconciliation only. If full AR tracking is actually needed, that's
  a separate contract to design deliberately, not something to retrofit here.
- **Refunds** — `CashMovementType.CashRefundTender` exists in the domain model
  (forward-compatibility, same reasoning as `Invoice.SourceQuoteId`) but nothing
  generates one yet; no refund flow has been built in any phase so far.

---

## 6. Reporting contract

`Lumina.Contracts.Reporting.IReportingService` — dashboard/report screens. Fully
implemented and tested. Every figure is computed LIVE by querying the actual
Sale/CashMovement/StockMovement records at request time — there is no cache, no
separately-maintained running total, anywhere in this implementation. This is
what "daily report matches raw sale data" means as a guarantee, not just a goal.

### 6.1 What UI can assume

- `GetDailySalesReportAsync` uses the **store's local calendar day**
  (`Store.TimeZoneId`), not a raw UTC day — same timezone discipline as fiscal
  record generation elsewhere in this codebase.
- `IDailySalesReport`'s internal consistency is guaranteed:
  `GrossSubtotal + VatTotal == GrandTotal`, and
  `CashTotal + CardTotal + OtherTenderTotal == GrandTotal` — UI can trust these
  identities without independently re-summing.
- `IRegisterSessionReport.LiveExpectedCash` uses the **exact same formula**
  (`OpeningFloat + sum of movements`) that `RegisterSession.Close()` uses to
  compute `ExpectedClosingCash` — for an open session this previews what
  closing right now would compute; for a closed session it equals the
  persisted value exactly. Useful for an "X-report" (mid-shift check) as well
  as reviewing a past "Z-report" (closed session).
- `GetStockOnHandAsync` returns only products with at least one recorded
  movement — a product that's never been received/sold at this store simply
  isn't in the list, not present with a zero.

### 6.2 Not built

- **No date-range reports beyond a single day** — `TopProductsRequest` and
  `DailySalesReportRequest` both take a single `DateOnly`, not a range. Weekly/
  monthly rollups would currently mean calling the daily report repeatedly and
  summing client-side, which is architecturally fine (same live-query
  guarantee applies to each call) but not yet a dedicated single-call contract.
- **No export (CSV/PDF) contract** — these interfaces return structured data
  for on-screen display; producing a downloadable report file is a separate,
  not-yet-designed concern.

---

## 7. Device contracts (printer, scale)

`Lumina.Contracts.Devices.IReceiptPrinter` / `IScaleService`. **Both are
currently registered against EMULATED implementations** — no real hardware
driver exists yet. Full details and why in `docs/PHASE8_NOTES.md`; summary
here.

### 7.1 Barcode scanners — no port, by design

Not part of this contract at all. Most retail barcode scanners are USB/
Bluetooth "keyboard-wedge" devices — they type the scanned barcode directly
into whatever text field has focus, exactly like a keyboard. This already
works through the existing `IPosSaleService.AddLineAsync` barcode search box
with zero backend integration. Don't build or expect an `IBarcodeScanner`
interface unless a specific device that needs one is identified.

### 7.2 `IReceiptPrinter` — what UI can assume

- `ReceiptDocument` is a small, printer-appropriate model (text lines with
  alignment/bold, separators, a QR marker, a cut signal) — **not** the same
  thing as the on-screen ticket preview UI renders in XAML. Build/send a
  `ReceiptDocument` separately for the physical print action; don't try to
  reuse on-screen rendering logic for it.
- `Lumina.Application.Devices.ReceiptDocumentBuilder` builds a correct
  `ReceiptDocument` from a real `Sale` + `Tenant` + `Store` + QR payload —
  prefer this over hand-building one, so formatting stays consistent.
- **Currently emulated**: `PrintAsync` always succeeds and just holds the
  document in memory (`SimulatedReceiptPrinter.PrintedReceipts`). No real
  paper, no real printer. `GetStatusAsync` always reports `Connected`. Do not
  build UI logic that assumes print failures are impossible — the interface
  supports `PrintStatus.Failed` precisely because a real adapter will need it;
  the emulation just never exercises that path.

### 7.3 `IScaleService` — what UI can assume

- `ReadWeightAsync` returns `null` on failure/timeout — UI shows a retry
  state, never fabricates a placeholder weight.
- `WeightReading.IsStable` should gate whether UI accepts the reading — real
  scales settle over a moment; the emulation always returns `true` (nothing to
  settle), so this can't be tested against the emulation alone.
- Integration is via the **existing** `AddLineRequest.Quantity` (already
  `decimal`) — no new POS-sale contract surface was needed for weighed items.
- **Currently emulated**: returns a randomized plausible test weight
  (0.1-2.1 kg), not a real reading.

---

## How to propose the next contract

Open a PR touching only `docs/CONTRACTS.md` (add a new numbered section) + the
corresponding new file under `src/Lumina.Contracts`, get it reviewed, merge, then
branch. Keep each contract's UI-facing read models separate from backend-internal
domain types — the contract should be the minimum shape the UI needs, not a leaky
projection of the underlying aggregate.
