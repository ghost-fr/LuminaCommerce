# Lumina Commerce — UI/UX Wireframes

Modern Avalonia UI equivalent of the GesVent 8 UI blueprint, adapted for
Lumina Commerce (.NET 8 / Avalonia POS with VeriFactu/AEAT compliance).

## Why templates, not 150 individual screens

GesVent 8 itself was built from a handful of shared patterns
(`BaseSelectionForm`, `BaseEditionForm`, `BaseInformesForm`,
`FacturacionScreen`) reused across ~150 concrete forms. This wireframe
set ports those same four patterns to Avalonia.

| Legacy base class | New template | Covers |
|---|---|---|
| *(none — new)* | `Views/Dashboard/DashboardView.axaml` | Landing KPIs |
| `MainForm` (MDI shell) | `Views/Shell/MainShellView.axaml` | App shell, nav, tabs |
| `BaseSelectionForm` | `Views/Shared/SelectionViewBase.axaml` | List/search screens |
| `BaseEditionForm` | `Views/Shared/EditionViewBase.axaml` | Create/edit screens |
| `BaseInformesForm` | `Views/Reports/ReportViewBase.axaml` | Report screens |
| TPV | `Views/Pos/PosCheckoutView.axaml` | Live checkout wireframe |

## Status

Wireframe-level AXAML — not compiled or bound to ViewModels.
Imported from Drive `files.zip` (Claude, 2026-09-14).

See `INTEGRATION_WITH_LIVE_POS.md` for how this relates to the live dark POS.
