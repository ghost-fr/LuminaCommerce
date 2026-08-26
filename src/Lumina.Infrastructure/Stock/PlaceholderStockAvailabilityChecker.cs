using Lumina.Application.Ports;

namespace Lumina.Infrastructure.Stock;

/// <summary>
/// Phase 3 placeholder — always returns available. The real append-only stock
/// ledger (blueprint's stock module) is Phase 4 scope. This exists so
/// PosSaleService's StockUnavailable rejection PATH is wired and testable now
/// (see PosSaleServiceTests), even though nothing currently makes it trigger in
/// a real running app. Swap the DI registration for the real Phase 4
/// implementation when it lands — IStockAvailabilityChecker's signature shouldn't
/// need to change.
/// </summary>
public sealed class PlaceholderStockAvailabilityChecker : IStockAvailabilityChecker
{
    public Task<bool> IsAvailableAsync(Guid productId, decimal requestedQuantity, CancellationToken ct = default) =>
        Task.FromResult(true);
}
