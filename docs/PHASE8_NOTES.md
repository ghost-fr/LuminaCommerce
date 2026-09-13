# Phase 8 — Device Ports (Printer, Scale)

## Decision

Build clean ports (`IReceiptPrinter`, `IScaleService`) and **emulated** drivers.
Do not ship guessed ESC/POS or scale serial protocols.

Roadmap gate: "real **or emulated** device round-trip" — satisfied by simulation.

## Wired in main tree

- `Lumina.Contracts.Devices` — document model + interfaces
- `SimulatedReceiptPrinter` / `SimulatedScaleService` — DI default
- POS UI: prints after successful cobro; scale sets qty for weighed lines; status strip shows device state

## Not done

1. Real ESC/POS driver (needs printer model + command reference)
2. Real scale driver (needs model + serial protocol)
3. Device discovery / COM-port UI
4. Barcode scanner port — **intentionally none** (keyboard-wedge)

## Swap to real hardware

Change only DI registrations for `IReceiptPrinter` and `IScaleService`.
