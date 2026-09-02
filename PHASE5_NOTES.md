# Phase 5 — Commercial Documents (Quote → Invoice)

## Scope decision: Quote + Invoice fully built and tested; CreditNote deferred

The roadmap lists "commercial documents state machine" as one phase covering
quote/invoice/credit-note. I scoped this delivery to **Quote and Invoice only**,
complete and tested, and deliberately did NOT build CreditNote (factura
rectificativa) alongside it — better to ship one complete, correct slice than
three rushed ones. CreditNote is a natural fast-follow using the exact same
pattern established here (aggregate → contract → application service →
fiscal wiring → JSON-projection repository → tests).

## What's implemented and tested

- **`Quote`** (Domain.Commercial) — mutable while `Draft`, full state machine
  (`Draft → Sent → Accepted/Rejected/Expired`, plus `ConvertedToInvoice`).
  NOT a fiscal document — no VeriFactu record at any point in a Quote's life.
- **`Invoice`** (Domain.Commercial) — immutable core (lines/totals/customer),
  mutable `Status`/`CancellationVeriFactuRecordId` for the cancel path. IS a
  fiscal document — every issuance generates a real registration record.
- **`QuoteService.ConvertToInvoiceAsync`** — the fiscal-critical operation this
  phase is really about. Same idempotency-key discipline, same atomic-commit
  discipline (Quote marked converted + Invoice added + fiscal record generated,
  all in one unit of work) as `PosSaleService.CompleteSaleAsync`.
- **`InvoiceService.CancelAsync`** — generates a real "RF de anulación" record.
  This is the first Application-layer caller of
  `HashChainService.ComputeCancellationHash` / `IFiscalRecordGenerator.
  GenerateCancellationAsync` — that mechanism was built and verified during the
  hash-chain fix but had no caller until now. **Gap closed.**
- Contracts: `IQuoteService`, `IInvoiceService` in `Lumina.Contracts.Commercial`
  — ready for Grok to build quote-builder and invoice-list screens against.
- Tests: 15 domain tests (Quote state machine, Invoice lifecycle), 8 application
  tests (full Draft→Invoice flow, rejection paths, idempotency replay,
  cancellation including the empty-reason and already-cancelled rejection paths).

## Flagged decisions and gaps — same honesty as every prior phase

1. **`TipoFactura = "F1"`** for converted invoices — reasoned (an Invoice always
   has a real customer, unlike a POS Sale's simplified-ticket default of `"F2"`),
   but **not independently confirmed** against the AEAT L2 invoice-type code
   list, same caveat as `PosSaleService`'s `"F2"`. Both need the same
   accountant/gestor confirmation flagged in `HASH_CHAIN_FIX_NOTES.md`.

2. **Cancellation reason isn't part of the fiscal hash.** AEAT's confirmed
   cancellation-record field set (Caso 3) has no free-text field. `reason` is
   required and stored at the Lumina level for audit purposes, but doesn't flow
   into `FiscalCancellationRequest`. If a reason needs to be fiscally recorded,
   it likely belongs elsewhere in the XML submission — not guessed at here.

3. **Chain-integrity pre-check still not implemented** — same gap as the
   hash-chain fix, now also applies to `GenerateCancellationAsync` since it
   shares the same chain-append logic as registration. One fix will resolve
   both call sites once `IVeriFactuChainStore` is extended to expose the
   previous-hash-of-previous-record needed for the check.

4. **CreateQuoteRequest takes `StoreId` explicitly**, not resolved via session/
   tenant context. Reasoning: Quote (like Cart) has no prior aggregate to
   inherit a StoreId from at creation time. This surfaced a pre-existing gap
   worth knowing about: **no contract anywhere defines how a `Cart` gets
   created either** — `IPosSaleService` only has `AddLineAsync`/etc. assuming a
   `cartId` already exists. Nothing broken by this (UI/composition-root
   presumably creates Carts directly via `ICartRepository`), but flagging it
   since Phase 5 is what made the gap visible.

5. **Quote rehydration replays the state machine** (`QuoteRepository.ToDomain`
   calls `Send()`/`Accept()`/etc. in sequence to reach the persisted status)
   rather than setting status directly — deliberate, since `Quote` has no
   "trust me, set this state" escape hatch, and shouldn't, so its invariants
   always run even on load from storage. Slightly unusual pattern, flagged so
   it doesn't look like a mistake on review.

6. **Same known QR-on-replay gap as Sale** (`PHASE2_3_NOTES.md` item 8) — a
   retried `ConvertToInvoiceAsync` call returns `QrPayload: null`. Not
   re-solved here; same reasoning (the real fix depends on the eventual
   ticket/invoice-reprint feature's shape, not yet decided).

7. **Invoice numbering has the same race condition** as POS ticket numbering
   (`PHASE2_3_NOTES.md` item 2) — counts existing rows, not safe under
   concurrent invoice issuance at the same store.

## What CreditNote will need, when it's picked up

Following the same pattern established here:
- `CreditNote` aggregate (Domain.Commercial) — immutable, references
  `OriginalInvoiceId`, requires a non-empty reason (same as cancellation)
- A `RectificationType`/AEAT rectification code (R1-R5) — **another code-list
  question needing accountant confirmation**, same category as `TipoFactura`
- `Invoice.MarkRectified()` already exists and is tested — ready for
  `CreditNoteService` to call after issuing the credit note
- Fiscal generation: a credit note is its own NEW registration record (`RF de
  alta` with a rectificative `TipoFactura`), NOT a cancellation record — this
  is a common point of confusion worth getting right: cancelling an invoice and
  crediting an invoice are legally distinct events with different record types
