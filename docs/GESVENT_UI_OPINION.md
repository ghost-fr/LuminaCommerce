# GesVent UI on Lumina — opinion and progress

Date: 30 August 2026  
Scope: `C:\Users\munir\Desktop\SupermarketPOS` compared with the GesVent 8 install on `E:\` (`MENU.XML`, TPV in `PERIFERICOS.XML`, WinForms in `GesVent80`).

## What Lumina already is

Lumina is **not** a clone of GesVent’s internals, and it should not become one. GesVent is a WinForms + DataSet + XML-config monolith. Lumina already has the right bones: Avalonia UI, capability-gated session, `PosSaleService`, pricing, VeriFactu hash chain, EF persistence.

**Working UI today**

| GesVent | Lumina | Status |
|---|---|---|
| TPV ticket, keypad, cash/card, F-keys | `PosCartView` | Live — strongest screen |
| Artículos (ARTART) | `CatalogueView` | Live search/list |
| Almacén / stocks | `StockView` | Live list + adjust/count (stock checker still a stub) |
| Login / users | `LoginView` | Live |
| Compras / Ventas documentos / Informes / Tablas / Sistema | Module placeholders | **Map only** — no documents yet |
| Back office | `Lumina.BackOffice.UI` | Same visual shell, not wired to services |

The till already copies GesVent **cashier muscle memory**: store/caja/ticket/cajero strip, scan + qty, line grid, department pad, numeric keypad, F8–F12 tenders, receipt preview, VeriFactu panel. That is the right TPV shape.

## What I changed in this pass

- Shared **navy / teal** theme (`Lumina.Shared.Controls/GesVentTheme.axaml`) — GesVent’s explorer-bar language, not 2005 grey, not generic Fluent dark.
- POS **left module rail** matching `MENU.XML`: TPV, Artículos, Almacén, Compras, Ventas, Informes, Tablas, Sistema.
- Live screens restyled (Spanish labels, cards, ARTART / SISLIS keys as orientation only).
- Unbuilt modules open an **honest placeholder** with GesVent keys (`COM`, `VENFAC`, …). They do not invent data or pretend a sale went through.
- Back office window now uses the same rail so the two apps feel like one product.

Commands, cart math, and `IPosSaleService` were **not** rewritten.

## Opinion — do this, don’t do that

**Keep the GesVent information architecture.** Cashiers and grocers already know TPV vs Artículos vs Almacén vs Compras vs Ventas (mayor). Renaming those modules to “Catalog / Inventory / Checkout” would be a product mistake in this market.

**Do not port GesVent UI technology.** No Infragistics explorer XML, no `EjecutaMenu(Key)` string soup, no `CONFIG.XML` as the runtime brain. Keys like `ARTART` are useful as *traceability* (this screen = that GesVent command), not as a dispatcher.

**TPV vs back office should stay two windows** (as now). GesVent mixed them in one EXE; modern ops want a full-screen till that never shows purchase invoices, and a back office that never shows a keypad.

**The department pad (Fruta, Pan, …) is still decoration.** In GesVent those buttons loaded PLUs/secciones. Wire them to catalogue families when families exist; until then they teach the layout without lying about stock.

**Highest-value next work (function, not chrome)**

1. Families/secciones behind the TPV pad (real ARTSEC / ARTFAM).
2. Park/recover ticket, cash drawer, customer lookup (F3) — the function bar already names them.
3. Ticket numbering sequence (race flagged in `PHASE2_3_NOTES.md`).
4. Purchasing/sales **documents** as a state machine, not a second cart.
5. Back office: same catalogue/stock VMs as POS, then TABCLI.

**Visual direction:** light management surfaces + dark till is correct. Do not flatten the TPV into a white web form; cashiers need large totals, a keypad, and a paper-like receipt.

## Verdict

The project is **ahead on fiscal/sale architecture** and **behind on GesVent’s full module surface**. That is the right order. This UI pass makes the product *look and navigate* like a modern GesVent so the missing modules are obvious. Filling Compras/Ventas/Informes with fake grids would be worse than placeholders.

If you only have time for one follow-up after this: **make the TPV department pad load real products**. That is the difference between “looks like GesVent” and “works like GesVent.”
