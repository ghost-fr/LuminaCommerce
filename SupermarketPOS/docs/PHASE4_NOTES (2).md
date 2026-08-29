# Phase 4 — Stock Ledger, plus the Catalogue Tenant-Wiring Fix

This patch bundles two things: the small catalogue fix I said I'd do (unblocks
Grok's product-browsing screens), and Phase 4 (stock ledger, replacing the Phase 3
placeholder that let every sale succeed regardless of real inventory).

## Catalogue fix

`ProductCatalogueService.SearchAsync`/`FindByBarcodeAsync` are now fully implemented
against `ICurrentTenantProvider` (same pattern as `AuthService`). The old internal
workaround method (`FindByBarcodeInternalAsync`) is gone — `PosSaleService` now calls
the public `FindByBarcodeAsync` directly, and its constructor lost the
`ICurrentTenantProvider` parameter it only needed for that workaround.
`docs/CONTRACTS.md` §3.3 updated to reflect this is resolved.

## Stock ledger (Phase 4)

**Scope decision, since you didn't specify:** single-store tracking only. No
multi-store transfers, no cross-store visibility yet — `StockMovement` is
`(StoreId, ProductId, QuantityDelta)`, and on-hand quantity is always the SUM of
movements for that exact store+product pair. If you need multi-store transfers,
that's an additive change (a `Transfer` reason that writes two movements, one negative
at the source store and one positive at the destination) — flag it and I'll add it
rather than you working around the current shape.

**What's implemented:**
- `StockMovement` — append-only domain entity, `Reason` enum (`Sale`,
  `ManualAdjustmentIncrease/Decrease`, `Receiving`, `InitialStock`), never zero-quantity,
  `Sale` reason requires a `ReferenceId` (the SaleId).
- `StockLedgerService` — `GetQuantityOnHandAsync`, `RecordSaleMovementsAsync` (called
  by `PosSaleService`), `AdjustAsync` (ready for the Phase 4 stock-adjustment UI
  screens, not yet built — that's a UI task for whoever picks up that roadmap item).
- `LedgerBackedStockAvailabilityChecker` — the real `IStockAvailabilityChecker`, now
  registered in DI in place of the Phase 3 placeholder. **Every sale now actually
  checks real stock and decrements it on completion** — this was the biggest
  correctness gap sitting in what was already merged.
- `PosSaleService` updated: stock check now passes `StoreId` (interface signature
  changed — `IStockAvailabilityChecker.IsAvailableAsync` gained a `storeId` param,
  since stock is per-store); stock movements are recorded in the SAME unit of work as
  the `Sale` and the VeriFactu chain link, right before the single final
  `SaveChangesAsync` — deliberately, so a crash between "sale committed" and "stock
  decremented" can't happen (both commit together or neither does).
- 5 new domain/application tests plus one new `PosSaleServiceTests` case
  (`CompleteSaleAsync_HappyPath_RecordsStockMovement`) asserting the actual decrement
  happens, not just that the flow doesn't crash.

## Flagged limitations — same honesty as prior phases

1. **Availability checks aren't safe under real concurrency.** `GetQuantityOnHandAsync`
   sums committed rows in the DB — it doesn't see another in-flight request's
   not-yet-saved movements. Two registers selling the last unit of the same product
   at the same instant could both pass the availability check. Same category of issue
   as the ticket-numbering race flagged in Phase 2/3 — needs a proper transaction/lock
   strategy before this goes near a multi-register store. Not fixed here; flagging
   honestly rather than pretending single-request correctness implies concurrent
   correctness.

2. **No stock-adjustment or receiving UI yet.** `StockLedgerService.AdjustAsync` is
   ready and tested, but nothing calls it yet outside tests — that's the Phase 4 UI
   scope (stock adjustment/count screens) per the original roadmap, still open.

3. **`IStockAvailabilityChecker`'s signature changed** (added `storeId`). This is an
   Application-internal port, not one of the frozen UI-facing contracts in
   `CONTRACTS.md`, so this didn't require a contract negotiation — but flagging it
   here in case anything else in flight referenced the old two-argument signature.
