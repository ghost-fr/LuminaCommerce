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
| POS sale (`IPosSaleService`, commands) | Frozen v1 | This doc, §2 |
| Product catalogue read model | Not yet specified | Phase 2 |
| Commercial documents | Not yet specified | Phase 5 |

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

---

## 3. How to propose the next contract (product catalogue, Phase 2)

Same process: open a PR touching only `docs/CONTRACTS.md` (add a `§4 Product
catalogue` section) + the corresponding new file under `src/Lumina.Contracts`, get it
reviewed, merge, then branch. Keep each contract's UI-facing read models separate from
backend-internal domain types — the contract should be the minimum shape the UI needs,
not a leaky projection of the `Product` aggregate.
