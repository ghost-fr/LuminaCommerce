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
        return await _db.StockMovements
            .Where(m => m.StoreId == storeId && m.ProductId == productId)
            .SumAsync(m => (decimal?)m.QuantityDelta, ct) ?? 0m;
    }

    public async Task AddMovementAsync(StockMovement movement, CancellationToken ct = default)
    {
        await _db.StockMovements.AddAsync(movement, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandByProductAsync(Guid storeId, CancellationToken ct = default)
    {
        // NEW as of Phase 7 — backs ReportingService's stock-on-hand report.
        // Grouped SUM per product, single query rather than N calls to
        // GetQuantityOnHandAsync for a store with many products.
        var grouped = await _db.StockMovements
            .Where(m => m.StoreId == storeId)
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(m => m.QuantityDelta) })
            .ToListAsync(ct);

        return grouped.ToDictionary(x => x.ProductId, x => x.Total);
    }
}
