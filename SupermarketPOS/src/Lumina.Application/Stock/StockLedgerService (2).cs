using Lumina.Application.Ports;
using Lumina.Domain.Stock;

namespace Lumina.Application.Stock;

/// <summary>
/// Application-layer service wrapping the stock ledger. Both the sale-completion
/// path (PosSaleService) and future stock-adjustment UI (Phase 4 UI scope, not yet
/// built) go through this — never write to IStockLedgerRepository directly from
/// elsewhere, or the "every change is a StockMovement" invariant gets easy to break.
/// </summary>
public sealed class StockLedgerService
{
    private readonly IStockLedgerRepository _ledger;

    public StockLedgerService(IStockLedgerRepository ledger) => _ledger = ledger;

    public Task<decimal> GetQuantityOnHandAsync(Guid storeId, Guid productId, CancellationToken ct = default) =>
        _ledger.GetQuantityOnHandAsync(storeId, productId, ct);

    /// <summary>Records one negative movement per sale line. Called by
    /// PosSaleService AFTER a sale is validated as completable but BEFORE the
    /// final SaveChangesAsync — so the movements land in the same atomic commit
    /// as the Sale row and the VeriFactu chain link. Does not itself check
    /// availability; that's IStockAvailabilityChecker's job, called earlier in
    /// the same flow — this method trusts the caller already validated.</summary>
    public async Task RecordSaleMovementsAsync(
        Guid storeId, Guid saleId, IEnumerable<(Guid ProductId, decimal Quantity)> lines, CancellationToken ct = default)
    {
        foreach (var (productId, quantity) in lines)
        {
            var movement = new StockMovement(
                Guid.NewGuid(), storeId, productId, -quantity, StockMovementReason.Sale, saleId);
            await _ledger.AddMovementAsync(movement, ct);
        }
    }

    /// <summary>Manual stock adjustment — Phase 4 UI (stock adjustment screens,
    /// per the roadmap) will call this once built. Delta must be nonzero; sign
    /// determines Increase vs Decrease reason automatically. Caller must call
    /// SaveChangesAsync afterward (via whichever repository's SaveChangesAsync —
    /// they share one DbContext) — this method only tracks the movement.</summary>
    public async Task AdjustAsync(Guid storeId, Guid productId, decimal delta, CancellationToken ct = default)
    {
        if (delta == 0m)
            throw new ArgumentException("Adjustment delta cannot be zero.", nameof(delta));

        var reason = delta > 0m ? StockMovementReason.ManualAdjustmentIncrease : StockMovementReason.ManualAdjustmentDecrease;
        var movement = new StockMovement(Guid.NewGuid(), storeId, productId, delta, reason, referenceId: null);
        await _ledger.AddMovementAsync(movement, ct);
    }
}
