using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumina.Infrastructure.Persistence.Configurations;

public class CartRecordConfiguration : IEntityTypeConfiguration<CartRecord>
{
    public void Configure(EntityTypeBuilder<CartRecord> b)
    {
        b.ToTable("Carts");
        b.HasKey(c => c.Id);
        b.Property(c => c.LinesJson).HasColumnType("TEXT");
    }
}

public class SaleRecordConfiguration : IEntityTypeConfiguration<SaleRecord>
{
    public void Configure(EntityTypeBuilder<SaleRecord> b)
    {
        b.ToTable("Sales");
        b.HasKey(s => s.Id);
        b.Property(s => s.TicketNumber).IsRequired().HasMaxLength(50);
        b.Property(s => s.LinesJson).HasColumnType("TEXT");
        b.Property(s => s.TendersJson).HasColumnType("TEXT");
        // Sale is append-only/immutable per domain invariant — no updates expected.
        // Index for the idempotency-key lookup (hot path on every completion retry):
        b.HasIndex(s => s.IdempotencyKey).IsUnique();
        b.HasIndex(s => new { s.StoreId, s.TicketNumber }).IsUnique();
    }
}

public class RegisterConfiguration : IEntityTypeConfiguration<Lumina.Domain.Sales.Register>
{
    public void Configure(EntityTypeBuilder<Lumina.Domain.Sales.Register> b)
    {
        b.ToTable("Registers");
        b.HasKey(r => r.Id);
        b.Property(r => r.Name).IsRequired().HasMaxLength(100);
    }
}

public class VeriFactuChainRecordConfiguration : IEntityTypeConfiguration<VeriFactuChainRecord>
{
    public void Configure(EntityTypeBuilder<VeriFactuChainRecord> b)
    {
        b.ToTable("VeriFactuChain");
        // Composite key: one row per record, ordered by CreatedAt within a boundary.
        b.HasKey(v => v.RecordId);
        b.HasIndex(v => new { v.SifBoundaryId, v.CreatedAt });
    }
}
