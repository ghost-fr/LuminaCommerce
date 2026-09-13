# Promote nested GAPFIXES into root `src/`

Commit `26d3069` (“new updates”) dropped a correctness-hardening pass under
**`SupermarketPOS/`** — not under root **`src/`**. The solution builds from root.

## What is already in root (UI + session bootstrap)

On `feat/ui/through-phase4`:

- `IPosSaleService.CreateCartAsync` / `GetOpenRegisterIdAsync`
- `IRegisterRepository.FindOpenByStoreIdAsync`
- POS shell bootstraps open register + cart
- Local demo cart remains fallback when backend fails

## What is still only under `SupermarketPOS/`

Do **not** copy `DependencyInjection (2).cs` wholesale until these exist in root:

| Nested piece | Needs in root |
|--------------|---------------|
| Hardened `PosSaleService` (UoW, stock ledger, cash drawer) | `StockLedgerService`, `CashDrawerService`, `IUnitOfWork` |
| `NumberSequenceGenerator` | DbContext `NumberSequences` + migration |
| Chain integrity (`GetRecentLinksAsync`, `PreviousHash`) | Updated `IVeriFactuChainStore` + `FiscalRecordGenerator` |
| `QuoteService` / `InvoiceService` | Commercial ports + repos |
| `ReportingService` | Contracts + implementation |
| `Lumina.Infrastructure.Tests` | Add to root `.sln` |

## Safe promote order

1. Ports: `IUnitOfWork`, extended `IFiscalRecordGenerator` / `IVeriFactuChainStore`
2. Infra: `UnitOfWork`, `NumberSequenceGenerator`, chain store columns + **migration**
3. Fiscal: generator with integrity check
4. Application: ledger-backed stock, cash drawer, then replace `PosSaleService`
5. DI: merge registrations (no `(2)` filename)
6. Tests + CI
7. Delete or archive `SupermarketPOS/` nest to avoid dual sources of truth

## UI expectation after promote

- Real barcode → real catalogue price (seed products required)
- F12 with open register → real ticket + QR from fiscal layer
- Rejection codes surface in status (already wired)
