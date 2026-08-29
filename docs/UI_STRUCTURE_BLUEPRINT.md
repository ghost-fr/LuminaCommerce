# GesVent 8 UI Structure Blueprint

> **Scope:** documentation only. This file describes the UI structure observed in the source tree and project references. No application files were changed.
>
> **Evidence convention:** `Verified` means visible in source or designer-generated code. `Unknown / Requires Verification` means the available source does not establish the detail, or the detail is implemented in a referenced binary.
>
> **Lumina note:** This blueprint is the structural spine for the Avalonia UI in this repo.
> Modern visual implementation lives under `src/Lumina.Pos.UI` (and related UI projects).
> Do not remove, merge, rename, or invent major modules/screens without updating this document.

## 1. Application Overview

### Verified technology

- Desktop WinForms application targeting .NET Framework 4.0, x86, with `AppLoader.Main` as the entry point.
- `AppLoader` enables visual styles, creates `MainForm`, and enters `Application.Run`.
- `MainForm` is the application shell and an MDI parent in practice. Child screens assign `MdiParent` to `MainForm` or its top-level control.
- UI controls are principally Infragistics 16.2: `UltraExplorerBar`, `UltraToolbarsManager`, `UltraStatusBar`, `UltraGrid`, `UltraTabControl`, `UltraDockManager`, `UltraSchedule`, `UltraTree`, `UltraCombo`, `UltraButton`, `UltraLabel`, and related controls.
- ActiveReports supplies report viewing/design/printing. `GV8Controls` supplies custom labels, text boxes, message boxes, selection helpers, progress/busy UI, and other shared controls.
- There is no evidence of a ViewModel layer, XAML, MVC/MVVM framework, or `INotifyPropertyChanged`-based presentation model in the inspected source. Screens directly coordinate controls, typed datasets, and `GV8Data`/`GV8IData`.

## 2. Hierarchy

```text
Application
├── AppLoader
├── MainForm (shell / MDI parent)
│   ├── Header and branding
│   ├── Main toolbar/menu
│   ├── Quick-access left panel
│   ├── MDI center content area
│   ├── MDI child selector
│   ├── Busy/progress overlay
│   └── Status bar and messages
├── Configuration and startup
│   ├── SplashForm
│   ├── Asistente configuration wizard
│   ├── GV8Configuracion forms
│   └── ElegirTiendaForm
├── POS / sales tickets
│   ├── VisorTpvScreen
│   ├── ticket restrictions and verification
│   ├── stores, tables, zones, registers, buttons
│   └── ticket and sales reports
├── Sales documents / invoicing
│   ├── orders
│   ├── delivery notes
│   ├── invoices
│   ├── budgets
│   ├── returns and transfers
│   └── receipts / collections / remittances
├── Product catalogue
│   ├── articles and fast article editing
│   ├── families, subfamilies, sections
│   ├── VAT, units, labels, properties, references, barcodes
│   └── supplier prices, offers, schedules, stock links
├── Pricing and promotions
│   ├── sale/purchase price lists
│   ├── supplier prices and offers
│   ├── promotions, discounts, loyalty points
│   └── price changes
├── Purchasing and suppliers
│   ├── purchase documents
│   ├── supplier master data
│   ├── price/offer imports
│   └── supplier comparisons
├── Inventory and logistics
│   ├── inventory counts and grouping
│   ├── stock centers and stock reports
│   ├── shrinkage
│   └── warehouse transfers
├── Customers and master data
│   ├── customers and customer-specific settings
│   ├── payment methods, banks, offices
│   ├── geographic data
│   └── customer receipts and points
├── Reports and analytics
│   ├── report selection/parameters
│   ├── report preview/printing/export
│   ├── sales, purchase, inventory, customer, supplier reports
│   ├── accumulated statistics
│   └── report designer and charts
├── Administration and security
│   ├── users and permissions
│   ├── companies and stores
│   ├── table access
│   ├── servers/remote terminals
│   └── schedules
├── Integrations and maintenance
│   ├── database backup/restore/update
│   ├── scale import/export
│   ├── SES/accounting transfer
│   ├── embedded web screens
│   └── biometric/device configuration
└── Cross-cutting dialogs and pickers
    ├── consultation/selection dialogs
    ├── date/store/company/entity pickers
    ├── confirmation/error/question messages
    └── busy/progress/help/search dialogs
```

## 3–11. Full detail

The complete screen specifications (shell, selection/edition/report/facturation templates,
module screen families, modal vs MDI classification, data coupling, exhaustive class
register, reusable components, verification gaps, and source anchors) are maintained
in the working copy of this blueprint as provided for the Lumina UI port.

**Canonical location in this repo:** `docs/UI_STRUCTURE_BLUEPRINT.md`

**UI mapping in code:** `src/Lumina.Pos.UI/Navigation/AppModules.cs` implements the
§2 hierarchy as the left-nav spine. Shell layout follows §3 MainForm regions
(header, left panel, center content, status bar).

When extending the Avalonia UI, prefer adding screens under existing blueprint modules
rather than inventing new top-level groups.
