namespace Lumina.Domain.Stock;

public enum StockMovementReason
{
    Sale,
    ManualAdjustmentIncrease,
    ManualAdjustmentDecrease,
    Receiving,
    InitialStock
}

/// <summary>
/// A single append-only ledger entry. Quantity on hand for a product at a store is
/// always the SUM of its movements, never a separately-mutated "current stock"
/// field — this is the blueprint's append-only stock ledger principle: every change
/// is a new row, nothing is ever updated or deleted. If a mistake needs correcting,
/// that's a new offsetting movement, never editing/removing a past one — same
/// immutability discipline as the Sale aggregate, for the same auditability reason.
/// </summary>
public class StockMovement
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid ProductId { get; private set; }

    /// <summary>Positive = stock added (receiving, positive adjustment, initial
    /// stock). Negative = stock removed (sale, negative adjustment). Never zero —
    /// a zero-quantity movement carries no information and is rejected.</summary>
    public decimal QuantityDelta { get; private set; }

    public StockMovementReason Reason { get; private set; }

    /// <summary>SaleId for Reason=Sale, null for manual adjustments/receiving
    /// (Phase 4 scope). Purchase-order id would go here once Receiving gets a real
    /// source document in a later phase.</summary>
    public Guid? ReferenceId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private StockMovement() { }

    public StockMovement(Guid id, Guid storeId, Guid productId, decimal quantityDelta, StockMovementReason reason, Guid? referenceId)
    {
        if (quantityDelta == 0m)
            throw new ArgumentException("A stock movement must have a nonzero quantity delta.", nameof(quantityDelta));
        if (reason == StockMovementReason.Sale && referenceId is null)
            throw new ArgumentException("A Sale-reason movement must carry the SaleId as ReferenceId.", nameof(referenceId));

        Id = id;
        StoreId = storeId;
        ProductId = productId;
        QuantityDelta = quantityDelta;
        Reason = reason;
        ReferenceId = referenceId;
        OccurredAt = DateTimeOffset.UtcNow;
    }
}
