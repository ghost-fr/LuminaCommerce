# Hash Chain Fix — VERIFIED Against Real AEAT Documentation

This supersedes the earlier "structural placeholder, unverified" warning on
`HashChainService.cs`/`QrPayloadBuilder.cs`. That warning is now largely resolved —
not by more careful guessing, but by working directly from AEAT's own worked
examples, pasted in by Ahmad from the actual PDFs, and independently re-computed
(SHA-256, by hand, outside the codebase) to confirm exact matches before any code
was written against them.

## What's now VERIFIED (not inferred, not guessed)

**Hash algorithm and format** — confirmed against **three** real AEAT worked
examples (Caso 1: first record with empty `Huella`; Caso 2: registration;
Caso 3: cancellation) from AEAT's "Algoritmo de cálculo de codificación de la
huella" document:
- SHA-256 over UTF-8 bytes of a query-string-style canonical form:
  `Campo=Valor&Campo=Valor...`, no trailing separator
- Output is **UPPERCASE** 64-char hex (the original implementation force-lowercased —
  confirmed as a real bug, now fixed)
- Field order and exact names confirmed for BOTH record types (see below)
- **All three AEAT examples form one continuous real chain** — Caso 1's output
  hash is Caso 2's `PreviousHash` input; Caso 2's output is Caso 3's. Reproducing
  all three from scratch, independently, gives AEAT's exact documented output at
  every link. This is the strongest verification level available short of an
  actual live AEAT sandbox call.
- Empty `Huella` (first record in a chain) confirmed to serialize as the literal
  substring `Huella=` (field name + `=` + nothing) — not omitted, not null-handled
  specially. Verified via Caso 1.

**Registration record ("RF de alta") fields**, in order:
`IDEmisorFactura, NumSerieFactura, FechaExpedicionFactura, TipoFactura, CuotaTotal,
ImporteTotal, Huella, FechaHoraHusoGenRegistro`

**Cancellation record ("RF de anulación") fields** — a genuinely different, smaller
field set, not a variant of registration:
`IDEmisorFacturaAnulada, NumSerieFacturaAnulada, FechaExpedicionFacturaAnulada,
Huella, FechaHoraHusoGenRegistro`

**QR payload** — confirmed against AEAT's own Java `codificarQR` example:
- Base URL, parameter names (`nif`, `numserie`, `fecha`, `importe`), order, and
  per-parameter URL-encoding all match exactly
- AEAT's example deliberately includes a literal `&` inside a parameter value —
  proving individual-parameter encoding is required, not naive concatenation. My
  original implementation already did this correctly (used `Uri.EscapeDataString`
  per field) — confirmed right, not a bug.
- Amount format confirmed as two decimals (`241.40`) — resolves the earlier
  uncertainty from a secondary source that claimed one decimal was correct. That
  secondary source was wrong; ignored now that a primary example confirms two.

## What's STILL open — flagged, not silently assumed

1. **Timestamp timezone.** Confirmed the *format* is `yyyy-MM-ddTHH:mm:sszzz` with
   an explicit offset like `+01:00` (not UTC/`Z`). NOT independently confirmed
   whether AEAT specifically requires the *store's local* timezone vs. any valid
   offset — I inferred "local, not UTC" from the field name itself
   (`FechaHoraHusoGenRegistro` — "huso horario" = timezone) and implemented
   `PosSaleService` to convert `Sale.CompletedAt` (UTC) to `Store.TimeZoneId`
   before building the fiscal request. Reasonable, not verified with the same
   rigor as the hash format itself.

2. **`TipoFactura` value.** AEAT's own example used `"F1"` (factura completa).
   `PosSaleService` now hardcodes `"F2"` (factura simplificada) instead —
   deliberately, on the reasoning that a supermarket POS ticket is far more
   likely to be a simplified invoice than a complete one, not because `F2` was
   confirmed anywhere. **This needs confirmation from an accountant/gestor
   familiar with the AEAT L2 invoice-type code list** before it's trusted for
   real invoices. Flagged as a `const string` with a comment at the call site,
   not buried.

3. **Chain-integrity pre-check not implemented.** AEAT's developer FAQ (art. 7.i
   of OM HAC/1177/2024) requires verifying, before generating any record past the
   first, that the previous record's OWN chain link is intact — not just reading
   its hash and trusting it. `IVeriFactuChainStore` currently only exposes the
   last hash, not enough to re-verify that record's own previous-hash field.
   `FiscalRecordGenerator.cs` has an explicit comment flagging this as
   unimplemented. Fixing it needs `IVeriFactuChainStore` to expose the last
   record's `(Hash, PreviousHash)` pair, plus the timestamp-monotonicity check
   from the same FAQ section (new record's timestamp can't be >1 minute *behind*
   the previous one).

4. **Production vs. sandbox QR base URL.** `prewww2.aeat.es` (confirmed from
   AEAT's own example) is their preproduction/test host. The real production
   host hasn't been confirmed from a production-specific document — swap this
   before any real, non-test invoice.

5. **Duplicate ticket-number prohibition, from the same FAQ.** AEAT explicitly
   forbids ever reusing an invoice/ticket number — including for test invoices,
   which must be properly cancelled (`RF de anulación`), not silently deleted or
   renumbered. This interacts with the ticket-numbering race condition already
   flagged in `PHASE2_3_NOTES.md` item 2, and with `Lumina.SeedDev`/manual test
   sales, which currently have no cancellation path at all. Not fixed here.

## What this means practically

The hash chain's *core mechanism* — the part where getting it wrong would mean
every single invoice fails AEAT validation — is now verified against real AEAT
data with 9 passing fixed-vector tests (3 hash values covering the first-record/
empty-Huella case, a mid-chain registration, and a cancellation; a full 3-record
chain reproduction test; a canonical-string format check; general-behavior checks)
plus 4 QR tests. That's the highest-risk part, and it's solid now.

What's NOT done: the cancellation/rectification workflow itself (nothing calls
`ComputeCancellationHash` from application code yet — that's Phase 5+), the
chain-integrity pre-check, and the two soft assumptions (timezone requirement,
invoice-type code) flagged above. None of those are "the hash chain is wrong" —
they're real, separate, bounded pieces of remaining work.
