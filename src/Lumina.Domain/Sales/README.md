# Sales aggregate (placeholder)

Phase 3 work item. Will contain:
- `Sale` aggregate root (immutable once completed)
- `SaleLine` value object
- Domain events: `SaleCompleted`, `SaleRejected`
- Invariants: totals must reconcile with line sum + VAT; a completed sale cannot be
  mutated, only superseded by a linked correction document (per VeriFactu §8.1).
