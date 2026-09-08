# Correctness-Hardening Pass — Chain Integrity, Atomic Numbering, Stock Race

This closes three of the concrete, code-fixable gaps from the earlier gap-triage:
the unbuilt AEAT chain-integrity pre-check, the ticket/invoice numbering race, and
the stock-availability race. All three are backed by tests — and where possible,
by **empirical verification**, not just code review reasoning. Confidence level is
stated explicitly per fix below, because they're not all equal.

## 1. Chain-integrity pre-check (AEAT OM HAC/1177/2024 art. 7.i)

**What it does:** before generating any VeriFactu record beyond the first in a
chain, verifies (a) the previous record's own `PreviousHash` actually matches the
record before *it* — catching tampering or corruption — and (b) the previous
record's timestamp isn't more than a minute ahead of the new one.

**Confidence:**
- Check (a), chain consistency: **high**. Structural, unambiguous, and the
  `VeriFactuChainRecord` schema now actually stores what's needed to check it
  (previously only `RecordHash` was stored — `PreviousHash` wasn't, so there was
  nothing to verify against even in principle).
- Check (b), timestamp tolerance: **lower confidence, explicitly flagged in code**.
  Built from a paraphrased reading of AEAT's developer FAQ, not a verbatim-verified
  worked example the way the hash algorithm itself was. If the exact wording/
  tolerance turns out to differ once re-checked against the primary PDF, only
  `FiscalRecordGenerator.VerifyChainIntegrityAsync`'s second check needs
  adjustment — the chain-consistency check is independent and stays correct.

**Wired in:** `FiscalChainIntegrityException` is caught in `PosSaleService`,
`QuoteService`, and `InvoiceService`, mapping to `RejectionCode.FiscalChainError`
(POS) or an ordinary `Rejected` result (Quote/Invoice) — nothing crashes uncaught.
6 new tests in `FiscalRecordGeneratorChainIntegrityTests`, including one that
actually tampers with a stored link and confirms the exception fires.

## 2. Atomic ticket/invoice numbering

**What it does:** replaces "count existing rows" (the flagged race since Phase 2/3)
with a single atomic SQL statement: `INSERT ... ON CONFLICT DO UPDATE ...
RETURNING`. One statement creates the row on first use or increments-and-returns
on every call after — no explicit transaction needed, because SQLite guarantees a
single statement's effects (including its `RETURNING` read) are atomic with
respect to other writers.

**Confidence: high — empirically verified, not just reasoned about.** Ran a real
reproduction (Python + real SQLite + real OS threads, outside .NET entirely, to
isolate the SQLite-level claim from any EF Core specifics):

```
30 concurrent callers, same store+sequence type
Got 30 results
Distinct values: 30
Sorted: [1, 2, 3, ..., 30]
Expected 1..30, no dupes, no gaps: True
```

Also verified: different stores get independent sequences (both start at 1);
different sequence types (Ticket vs Invoice) at the same store are independent.
The same scenario is re-proven inside real EF Core/SQLite in
`NumberSequenceGeneratorConcurrencyTests` (20 concurrent callers via real separate
`DbContext`/connection instances against a shared in-memory SQLite DB) — this is
the test that will actually run in CI going forward.

## 3. Stock-availability race

**What it does:** wraps `PosSaleService.CompleteSaleAsync`'s stock-check-through-
final-commit section in a real database transaction (`IUnitOfWork`, SQLite `BEGIN
IMMEDIATE`) — closing the race flagged since Phase 4, where two concurrent sales
of the last unit of a product could both pass the availability check before either
decremented.

**Confidence — split into two parts, stated honestly because they're not equally
verified:**

- **The core SQLite locking claim (high confidence, empirically verified):**
  `BEGIN IMMEDIATE` acquires a write-intent lock at BEGIN, not deferred until the
  first write — so a concurrent transaction's own `BEGIN IMMEDIATE` blocks until
  the first commits, and its *subsequent read* then sees the already-committed
  write, not stale data. Verified with a real Python + SQLite + threading
  reproduction:
  ```
  seller1: acquired lock, saw qty=5, decremented by 3 → SUCCESS
  seller2: acquired lock AFTER seller1 committed, saw qty=2 (updated!), REJECTED
  Final qty: 2 (correct — not oversold)
  ```
- **The EF-Core-specific mechanics (lower confidence — NOT independently run
  against real EF Core by me):** `UnitOfWork.cs` pins the connection open via
  `OpenConnectionAsync()` before issuing raw `BEGIN IMMEDIATE`, following
  Microsoft's documented pattern for manual transaction control — but I don't have
  the .NET SDK in this environment, so this specific code has never actually
  executed. **This is flagged explicitly in the class's own doc comment, not
  hidden.**

Because of that gap, I wrote (not just described) the actual verification that
would close it: `UnitOfWorkStockRaceTests` in the new `Lumina.Infrastructure.Tests`
project, using the real `StockLedgerRepository` and `UnitOfWork` (not fakes) against
a real in-memory SQLite database, simulating two and then ten concurrent "sales"
against limited stock and asserting the exact right number succeed and none
oversell. **This test has not been run by me** (again, no SDK) — it's the thing
that needs to actually pass in your CI before this fix is trusted. If it fails,
that's the EF-Core-connection-pinning assumption turning out wrong, not the
underlying SQLite behavior (which is separately, already proven).

## New: `Lumina.Infrastructure.Tests` project

First Infrastructure-level test project in the solution — needed because raw SQL
and real transaction/locking behavior can't be verified against fakes; that's the
whole point of these particular tests. **You need to add it to the solution
yourself:**

```bash
dotnet sln add tests/Lumina.Infrastructure.Tests/Lumina.Infrastructure.Tests.csproj
```

`ci.yml` already updated to run it as a blocking step (same tier as the fiscal
tests — these are the tests that verify the code doesn't silently oversell or
double-issue numbers, which is exactly the kind of thing worth blocking merges on).

## Migration reminder — this now matters more than before

Two schema changes: the new `NumberSequences` table, and `VeriFactuChainRecord`
gaining `PreviousHash`/`GeneratedAt` columns. Like every prior schema change,
`dotnet ef migrations add` needs to be run locally — but this is worth re-stating
because migrations have reportedly never actually been run against a real database
at all yet (flagged repeatedly across earlier phases). This patch is a good forcing
function to finally do that, since without it, none of the fixes in this pass can
be exercised for real.

## What this pass did NOT fix — still open, still needs what it always needed

- `TipoFactura` code confirmation (needs an accountant/gestor)
- Local-timezone requirement for fiscal timestamps (needs the same rigor applied
  to the timestamp tolerance check above — a primary-source worked example, not
  inference from a field name)
- QR production URL (only the sandbox host is confirmed)
- Real printer/scale hardware drivers (needs actual device models + protocol docs)
