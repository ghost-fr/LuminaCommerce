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
        b.HasIndex(s => s.IdempotencyKey).IsUnique();
        b.HasIndex(s => new { s.StoreId, s.TicketNumber }).IsUnique();
    }
}

public class TicketSequenceRecordConfiguration : IEntityTypeConfiguration<TicketSequenceRecord>
{
    public void Configure(EntityTypeBuilder<TicketSequenceRecord> b)
    {
        b.ToTable("TicketSequences");
        b.HasKey(t => t.StoreId);
        b.Property(t => t.NextNumber).IsRequired();
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
        b.HasKey(v => v.RecordId);
        b.HasIndex(v => new { v.SifBoundaryId, v.CreatedAt });
    }
}
