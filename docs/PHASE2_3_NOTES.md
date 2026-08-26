# Phase 2 & 3 — Catalogue, Pricing, POS Sale, VeriFactu Hash Chain

## What's implemented and tested

- **Catalogue**: `Product`, `TaxCategory`, `Promotion` (simple per-product % or fixed
  discount, no stacking/bundles). `PricingService` is the single source of truth for
  "what does this cost right now" — everything else must go through it.
- **Sales**: `Cart`/`CartLine` (mutable working state), `Sale`/`SaleLine` (immutable
  once created, hard-fails if tender total doesn't match sale total), `Register`
  (open/closed gate).
- **Fiscal**: `HashChainService` (SHA-256, documented canonical format, 6 fixed-vector
  tests with independently-computed hashes), `QrPayloadBuilder`, `FiscalRecordGenerator`
  tying both together with chain persistence.
- **`PosSaleService`**: the real `IPosSaleService` implementation — idempotency-key
  replay, register-open check, stock check (against a placeholder, see below), tender
  validation, ticket numbering, fiscal record generation, all wired together. 7 tests
  covering the happy path and every rejection code.
- **Infrastructure**: EF Core persistence for everything above, DbContext updated.

Test count this phase: 6 hash-chain fixed-vector tests, 3 QR builder tests, 7
PosSaleService tests, 3 PricingService tests, ~12 domain tests (Cart/Sale/Register).

## Flagged simplifications — read before Phase 4+ builds on top of these

1. **Stock check is a placeholder.** `PlaceholderStockAvailabilityChecker` always
   returns available. The `StockUnavailable` rejection path is fully wired and tested
   against this placeholder — swapping in the real Phase 4 stock ledger shouldn't
   require touching `PosSaleService` at all, just the DI registration.

2. **Ticket numbering has a race condition.** `SaleRepository.NextTicketNumberAsync`
   counts existing rows — correct for one register completing sales sequentially, NOT
   safe if two registers in the same store complete sales at the same instant (both
   could compute the same ticket number). Needs a real sequence/counter-with-lock
   before any multi-register store goes near this in production. Flagged in code too.

3. **QR payload is NOT verified against the current AEAT spec.** I wrote
   `QrPayloadBuilder` from general knowledge of the VeriFactu program's shape (a
   verification URL with NIF/invoice number/date/amount), not from the current
   published technical documentation. Before a single real invoice uses this: pull
   the current spec from AEAT's site and diff every field name, URL, and QR rendering
   rule (size/quiet-zone/error-correction) against what's here. Treat this as a
   structural placeholder. Same caution applies to the hash-chain field set in
   `VeriFactuRecordInput` — the *fields* chained are a best-effort match to the public
   VeriFactu description, not verified against the XSD/Reglamento.

4. **Cart/Sale persist via a JSON projection, not direct EF mapping.** Both
   aggregates use private setters and encapsulated collections (by design — rich
   domain model, not DataRows). Rather than hand-write an EF backing-field mapping I
   couldn't verify compiles (no `dotnet ef` available here), repositories translate
   between the domain aggregate and a flat/JSON-columned persistence record using
   each aggregate's public API. This works and is unit-testable, but run
   `dotnet ef migrations add` locally and sanity-check the generated schema — a JSON
   text column is a reasonable pattern but worth confirming it's what you want for
   `Sales`/`Carts` specifically vs. normalized line-item tables (mainly a reporting/
   query-ability tradeoff for Phase 7).

5. **`Sale` got a second constructor overload** (`completedAt`, `veriFactuRecordId`
   params) specifically so rehydrating a persisted sale doesn't silently overwrite its
   original completion timestamp with "now." If you see two `Sale` constructors and
   wonder why: this is why — use the simple one for new sales, the full one only for
   rehydration.

6. **`ProductCatalogueService.SearchAsync`/`FindByBarcodeAsync` throw
   `NotImplementedException`.** They need the same `ICurrentTenantProvider`
   composition-root wiring `AuthService` needs — not yet done. The internal method
   `FindByBarcodeInternalAsync(tenantId, ...)` is fully implemented and is what
   `PosSaleService` actually calls. See `docs/CONTRACTS.md` §3.3.

7. **VeriFactu chain append isn't in an explicit cross-context transaction.**
   `VeriFactuChainStore.AppendAsync` relies on sharing the same `DbContext`/unit-of-
   work as the `Sale` it belongs to (both flushed by `PosSaleService`'s final
   `SaveChangesAsync`). Fine today since everything's one `LuminaDbContext`. If Fiscal
   ever gets its own DbContext, this needs an explicit transaction spanning both, or a
   sale could persist without its chain link (or vice versa).

8. **Idempotent replay doesn't return the original QR payload.** `Sale` doesn't store
   the QR string, only `VeriFactuRecordId`. On a retried `CompleteSaleAsync` call with
   the same idempotency key, `PosSaleService` currently returns `QrPayload: null`.
   Deliberately not fixed yet — the right fix (store QR on `Sale` vs. add a lookup by
   record id) depends on how the Phase 6/7 ticket-reprint feature wants to work, and
   guessing now risks picking the wrong shape.

## Still needed before Grok can build POS cart/sale screens end-to-end

- Composition-root DI wiring (`App.axaml.cs`) registering all of Phase 1-3's services —
  next planned piece of work.
- `dotnet ef migrations add` run locally (Phase 1 gap, still open) — needed before any
  of this actually persists to a real SQLite file.
- A decision on item 6 above (tenant-provider wiring) since it blocks the public
  catalogue browsing contract, even though POS scan-to-cart already works via the
  internal method.
