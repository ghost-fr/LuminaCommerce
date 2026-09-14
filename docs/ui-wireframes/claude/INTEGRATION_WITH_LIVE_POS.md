# Claude wireframes vs live Lumina POS

Source: Drive `files.zip` (2026-09-14) — Claude UI/UX wireframe pack.

## What Claude delivered
- Light design system: Colors / Typography / Controls
- MainShellView (tabs, no MDI)
- DashboardView (KPIs)
- SelectionViewBase / EditionViewBase / ReportViewBase templates
- PosCheckoutView (light theme, Cash/Card/Split/Charge)
- ArticleSelectionView example

Status per Claude: **wireframe AXAML — not compiled against ViewModels**.

## Live POS today (Grok lane)
- `src/Lumina.Pos.UI/Views/PosCartView.axaml` on `feat/ui/through-phase4`
- Dark dense cashier workstation, Spanish labels, MODO SIMPLE
- Bound to `PosCartViewModel` (local + backend cart)

## How to use these together
1. Keep wireframes under `docs/ui-wireframes/claude/` as the **design reference** for non-POS modules (lists, edit, reports, dashboard).
2. Do **not** replace PosCartView wholesale with PosCheckoutView without porting bindings — Claude POS is English placeholder amounts (€55.90), not MVVM-bound.
3. Optional: merge Claude `Colors.axaml` tokens into App.axaml for **light theme** modules; keep dark theme for TPV if cashiers prefer contrast.
4. Concrete next screens: Customers/Suppliers lists = copy ArticleSelectionView pattern + SelectionViewBase.

## Recommendation
- **POS checkout**: continue evolving live `PosCartView` (already functional).
- **Back-office UI**: implement from Claude templates (shell tabs, selection, edition, dashboard).
