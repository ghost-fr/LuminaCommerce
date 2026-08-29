using Lumina.Application.Ports;

namespace Lumina.Infrastructure.Stock;

/// <summary>
/// SUPERSEDED as of Phase 4 by Lumina.Application.Stock.LedgerBackedStockAvailabilityChecker,
/// which is what's actually registered in DI now (see DependencyInjection.cs). Kept
/// here, updated to match the current IStockAvailabilityChecker signature, only so
/// nothing references a stale interface shape — not used anywhere. Safe to delete
/// once nobody has a reason to swap back to an always-available stub (e.g. certain
/// test/demo scenarios), otherwise harmless to leave.
/// </summary>
public sealed class PlaceholderStockAvailabilityChecker : IStockAvailabilityChecker
{
    public Task<bool> IsAvailableAsync(Guid storeId, Guid productId, decimal requestedQuantity, CancellationToken ct = default) =>
        Task.FromResult(true);
}
