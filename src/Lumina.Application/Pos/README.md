# POS application services (placeholder)

Phase 3 work item. Will contain the concrete implementation of
`Lumina.Contracts.Pos.IPosSaleService`, plus `CompleteSaleCommandHandler` orchestrating:
pricing resolution -> stock reservation -> tender validation -> Sale aggregate creation
-> Lumina.Fiscal hash-chain record generation -> outbox write -> commit.
See docs/CONTRACTS.md for the frozen interface this must satisfy.
