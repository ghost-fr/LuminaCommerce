# UI status & remaining work

Branch: `feat/ui/through-phase4`  
Lane: Grok = Avalonia POS UI · Claude = domain/infra/fiscal

## Done (UI)

| Area | Status |
|------|--------|
| Login + session shell | Working |
| GesVent-shaped nav (TPV, Artículos, Almacén, placeholders) | Working |
| Dense POS workstation (scan, lines, dept pad, keypad, pay, receipt, Veri*Factu panel) | Working |
| Department keys with prices (FRUTA…BEBIDAS) | Working — local demo prices |
| Local complete-sale + simulated print | Working |
| Keypad layout (no Avalonia RowSpacing) | Fixed |
| Shell bootstrap: `CreateCart` + open register | Wired |
| Backend COBRAR when real cart + open register | Restored (falls back to local) |
| Catalogue / Stock screens | Present (need seeded data) |
| Device status (printer / scale simulated) | Present |

## Not production-ready yet

1. **Empty product catalogue** — department keys use hardcoded demo prices; real barcodes need seeded products.
2. **Admin capabilities** — SeedDev must grant `pos.operate_register` (and related) or TPV is blocked.
3. **Veri*Factu “Local”** — fiscal registration only when backend `CompleteSale` succeeds with open register + real cart lines.
4. **Module placeholders** — Compras, Ventas, Informes, Tablas, Sistema are shells only.
5. **Nested `SupermarketPOS/` gapfixes** — not promoted into root `src/` (see `MERGE_NESTED_GAPFIXES.md`).
6. **Ticket-number race / real stock ledger / AEAT live host** — backend (Claude).
7. **UI_STRUCTURE_BLUEPRINT.md** — may still be missing from remote `docs/` if only local.

## Claude (backend) blockers for end-to-end fiscal sale

- Seed catalogue products + barcodes matching department or scan flow
- Ensure open **Register** after seed
- Grant **Admin** all POS capabilities on seed
- Promote nested gapfixes (`IUnitOfWork`, number sequence, chain integrity)
- Migrations applied on operator machine

## Grok next (optional polish)

- Client picker / observations dialogs (today status-only)
- Mixed tender amount split UI
- Register open/close screen (Phase 6)
- Back-office visual parity pass
- Keyboard shortcuts (F3–F12) wired to Avalonia key bindings

## How to test UI training mode today

```powershell
git pull origin feat/ui/through-phase4
dotnet build src\Lumina.Pos.UI
dotnet run --project src\Lumina.Pos.UI
```

Login → TPV → **PAN** / **CERVEZA** → **EFECTIVO** → **COBRAR** → receipt panel shows ticket.
