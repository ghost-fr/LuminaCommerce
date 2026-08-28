using Lumina.Application.Ports;

namespace Lumina.Application.Stock;

/// <summary>
/// Real IStockAvailabilityChecker implementation, backed by the append-only ledger.
/// Replaces the Phase 3 PlaceholderStockAvailabilityChecker (which always returned
/// true) in DI registration — PosSaleService's code didn't need to change at all,
/// only the DI wiring, which was the point of keeping this behind an interface.
/// </summary>
public sealed class LedgerBackedStockAvailabilityChecker : IStockAvailabilityChecker
{
    private readonly StockLedgerService _ledger;

    public LedgerBackedStockAvailabilityChecker(StockLedgerService ledger) => _ledger = ledger;

    public async Task<bool> IsAvailableAsync(
        Guid storeId, Guid productId, decimal requestedQuantity, CancellationToken ct = default)
    {
        var onHand = await _ledger.GetQuantityOnHandAsync(storeId, productId, ct);
        return onHand >= requestedQuantity;
    }
}
