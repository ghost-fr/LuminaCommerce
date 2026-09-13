# Phase 7 — Reporting Read Models

## Core design decision: live queries, never a cache

The phase gate is explicit: "daily report matches raw sale data." The only way
to guarantee that by construction, not by discipline, is to have every report
computed by querying the actual Sale/CashMovement/StockMovement rows at request
time — no materialized/denormalized reporting table, no running-total counter
maintained alongside the write path. `ReportingService` does exactly this: it
has no state of its own, every method is a fresh aggregation over
`ISaleRepository`/`ICashMovementRepository`/`IStockLedgerRepository`.

Tradeoff, worth knowing: this means report queries do real aggregation work on
every call rather than reading a precomputed value. Fine at SMB single-store
scale on SQLite; worth revisiting (e.g. a materialized daily-totals table,
recomputed on write) if a store's sales volume ever makes live aggregation
slow — but that's a performance optimization to make later with real data,
not something to guess at now.

## What's implemented and tested

- **`GetDailySalesReportAsync`** — sale count, gross/VAT/grand totals, and a
  tender-type breakdown (cash/card/other), all summed directly from the
  store's local-calendar-day Sale records. Tests assert totals against an
  independently-summed expectation (not just "the method returns something"),
  plus internal-consistency checks (subtotal+vat==total, tender breakdown
  sums to total).
- **`GetTopProductsAsync`** — quantity and revenue per product, aggregated
  across the day's `SaleLine`s.
- **`GetStockOnHandAsync`** — per-product on-hand quantity across an entire
  store (extends `IStockLedgerRepository`, which previously only supported a
  single-product lookup).
- **`GetRegisterSessionReportAsync`** — live cash reconciliation view, usable
  for both an in-progress session (an "X-report," a mid-shift check without
  closing) and a closed one (a "Z-report"). Test explicitly cross-checks that
  `LiveExpectedCash` uses the identical formula `RegisterSession.Close()` uses
  — this is the actual mechanism that guarantees the report can't silently
  diverge from what closing the register would compute.
- Three existing repository ports extended (not replaced) with new read-only
  query methods: `ISaleRepository.FindByStoreAndDateRangeAsync`,
  `IStockLedgerRepository.GetOnHandByProductAsync`,
  `ICashMovementRepository.GetForSessionAsync`. Every fake in every test file
  that implements these interfaces was updated to match — checked across all
  carried-forward test files, not just the ones directly touched this phase.
- 8 `ReportingService` tests, all built around genuinely independent
  expected-value assertions rather than tautological "matches what the method
  itself computed" checks.

## Timezone handling

Same discipline as fiscal record generation (Phase 2/3, Phase 5): the store's
local calendar day is converted to a UTC range using `Store.TimeZoneId` via
`TimeZoneInfo`, and the actual SQL-translatable query filters on that UTC range
— no timezone arithmetic happens inside the LINQ query itself (which could fail
to translate to SQL), all of it happens in C# before the query runs.

## Flagged gaps

1. **No date-range reports beyond a single day.** Weekly/monthly rollups
   aren't a dedicated contract yet — architecturally trivial to add (same
   live-query guarantee), just not built, since nothing in the roadmap
   specifically asked for it and I didn't want to guess at a shape (weekly?
   monthly? custom range?) without a real requirement.

2. **No export contract (CSV/PDF).** These interfaces return structured data
   for on-screen display only.

3. **Live-query performance is untested at scale.** Fine for a single store's
   single day of transactions on SQLite; no load testing has been done at any
   point in this project (flagged as a general gap since Phase 0), and this
   phase doesn't change that.
