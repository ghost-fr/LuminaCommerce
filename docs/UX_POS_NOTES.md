# POS UX redesign (2026-09-13)

## Design goals
- Cashier speed: scan / department → see line → pay → COBRAR
- Dense workstation (not SaaS dashboard)
- Simple POS mode: no Veri*Factu dominance
- Touch-friendly targets, Spanish labels, F-key hints

## Regions
1. Header: store, caja, ticket, cajero, devices, MODO SIMPLE, live TOTAL
2. Left: barcode, ticket table, department pad
3. Center: totals, keypad buffer, numeric pad, EFECTIVO/TARJETA/MIXTO, COBRAR
4. Right: paper receipt preview + ticket status
5. Bottom: F3–F7 style functions + balanza + nuevo ticket

## Next UX (optional)
- Keyboard shortcuts wired in code-behind
- Empty-state when Lines.Count == 0
- Error status in red via IsError binding
- Sound on COBRAR success
