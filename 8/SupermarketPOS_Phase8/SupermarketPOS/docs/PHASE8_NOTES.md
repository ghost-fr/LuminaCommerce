# Phase 8 — Device Ports (Printer, Scale)

## The core honesty issue this phase raises

Unlike every prior phase, this one touches physical hardware — and hardware
protocols are things I cannot verify or guess at safely, the same principle
that governed the VeriFactu fiscal work. Real receipt printers use ESC/POS (or
a manufacturer variant) command sets; real scales use serial protocols that
differ significantly between manufacturers (Toledo, CAS, Dibal, Mettler
Toledo, and others all have different, undocumented-to-me wire formats). I
have no confirmed spec for any specific device Ahmad's customers will actually
use.

**Decision made accordingly:** build the clean port abstraction (the
`IReceiptPrinter`/`IScaleService` interfaces) and fully-working **emulated**
implementations, satisfying the roadmap's own phase gate — "real **or
emulated** device round-trip" — with a genuine, tested emulated round-trip.
Do NOT attempt to write a real hardware driver against a guessed protocol,
which would risk shipping something that looks complete but silently fails
against real equipment, or worse, sends malformed commands to a real printer.

## What's implemented and tested

- **`IReceiptPrinter`** — a printer-appropriate document model
  (`ReceiptDocument`: text lines with alignment/bold, separators, QR marker,
  cut signal), deliberately distinct from the on-screen ticket preview (that's
  a UI/XAML concern — the earlier POS mockup already shows Grok building that
  independently). `SimulatedReceiptPrinter` "prints" by holding documents in
  memory; always reports `Connected`; always succeeds.
- **`IScaleService`** — returns a `WeightReading` (kilograms + stability flag).
  `SimulatedScaleService` returns a randomized plausible test weight. Existing
  `AddLineRequest.Quantity` (already `decimal`) is the integration point for
  a weighed item — no POS-sale contract change was needed.
- **`ReceiptDocumentBuilder`** — the piece that makes the "device round-trip"
  test mean something real: it builds a `ReceiptDocument` from an actual `Sale`
  aggregate (with real lines, real tenders, real totals, resolved product
  names via `IProductRepository`), not a hand-typed test fixture. The round-
  trip test constructs a real `Sale` the same way `PosSaleService` would,
  builds a receipt from it, sends it through the emulated printer, and asserts
  on the actual rendered content (ticket number, product name, total, NIF, QR
  payload all present and correct).
- **Barcode scanners explicitly get no port** — see `CONTRACTS.md` §7.1.
  Building a fake abstraction for hardware that doesn't need one would be
  worse than building nothing.
- 7 tests: printer round-trip (success + status), plain-text rendering
  content checks, scale reading behavior, and the full real-Sale-through-
  builder-to-emulated-printer test described above.

## What's explicitly NOT done — and what's needed before it can be

1. **No real ESC/POS printer driver.** Would need: the specific printer
   model(s) Ahmad's customers actually use, their command reference (most
   thermal printers support a common ESC/POS subset, but QR rendering,
   codepage/character encoding for Spanish accented characters, and cut
   command specifics vary), and ideally physical access to test against
   before shipping.

2. **No real scale driver.** Would need: the specific scale model(s), its
   serial protocol documentation (baud rate, query/response format, how it
   signals stability), and physical access to test.

3. **No device discovery/configuration UI.** Nothing here handles "which COM
   port / USB device / network address is the printer at" — that's real
   scope for whoever builds a real adapter, not addressed by the port
   interfaces themselves (which intentionally don't know or care about
   connection details).

4. **`GetStatusAsync` is meaningless against the emulation** — it always
   returns `Connected` because there's nothing to be disconnected from. Once
   a real adapter exists, this becomes an actually meaningful health check
   the UI's device-status indicator can rely on.

## If/when real hardware integration happens

The port interfaces were designed so that swapping emulated for real should
be a DI-registration change only (`DependencyInjection.cs` has both swap
points commented explicitly) — nothing in `PosSaleService`, the UI contracts,
or `ReceiptDocumentBuilder` should need to change. That's the whole point of
building this as ports & adapters rather than hardcoding a specific device
integration now.
