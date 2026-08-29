using Lumina.Domain.Stock;

namespace Lumina.Application.Ports;

public interface IStockLedgerRepository
{
    /// <summary>SUM of all movements for this product at this store. 0 if no
    /// movements exist yet (not an error — an unstocked new product is valid).</summary>
    Task<decimal> GetQuantityOnHandAsync(Guid storeId, Guid productId, CancellationToken ct = default);

    /// <summary>Tracks the movement on the shared unit of work — does NOT call
    /// SaveChanges itself. Caller (StockLedgerService, ultimately PosSaleService)
    /// is responsible for the final SaveChangesAsync, same pattern as every other
    /// repository in this codebase.</summary>
    Task AddMovementAsync(StockMovement movement, CancellationToken ct = default);
}
