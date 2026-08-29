using Lumina.Domain.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumina.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("StockMovements");
        b.HasKey(m => m.Id);
        b.Property(m => m.QuantityDelta).HasColumnType("decimal(14,3)");
        // Composite index for the hot-path query: sum of movements for a given
        // store+product (both availability checks and on-hand lookups hit this).
        b.HasIndex(m => new { m.StoreId, m.ProductId });
        // Append-only per the domain invariant (Domain/Stock/StockMovement.cs) —
        // no update/delete path is exposed by IStockLedgerRepository on purpose.
    }
}
