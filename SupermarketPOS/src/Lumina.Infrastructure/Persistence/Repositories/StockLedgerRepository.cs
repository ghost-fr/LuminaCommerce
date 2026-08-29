using Lumina.Application.Ports;
using Lumina.Domain.Stock;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

public class StockLedgerRepository : IStockLedgerRepository
{
    private readonly LuminaDbContext _db;
    public StockLedgerRepository(LuminaDbContext db) => _db = db;

    public async Task<decimal> GetQuantityOnHandAsync(Guid storeId, Guid productId, CancellationToken ct = default)
    {
        // NOTE: this queries the DATABASE, not the DbContext's in-memory change
        // tracker — a StockMovement added earlier in the SAME request (via
        // AddMovementAsync, before SaveChangesAsync) will NOT be counted here yet.
        // Doesn't affect PosSaleService today (availability is checked once, before
        // any movements for that sale are added), but matters if this method is
        // ever called after AddMovementAsync within the same unit of work — it
        // would undercount by the pending amount.
        return await _db.StockMovements
            .Where(m => m.StoreId == storeId && m.ProductId == productId)
            .SumAsync(m => (decimal?)m.QuantityDelta, ct) ?? 0m;
    }

    public async Task AddMovementAsync(StockMovement movement, CancellationToken ct = default)
    {
        await _db.StockMovements.AddAsync(movement, ct);
    }
}
