using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumina.Infrastructure.Persistence.Configurations;

public class QuoteRecordConfiguration : IEntityTypeConfiguration<QuoteRecord>
{
    public void Configure(EntityTypeBuilder<QuoteRecord> b)
    {
        b.ToTable("Quotes");
        b.HasKey(q => q.Id);
        b.Property(q => q.Status).IsRequired().HasMaxLength(30);
        b.Property(q => q.LinesJson).HasColumnType("TEXT");
        b.HasIndex(q => new { q.StoreId, q.Status });
    }
}

public class InvoiceRecordConfiguration : IEntityTypeConfiguration<InvoiceRecord>
{
    public void Configure(EntityTypeBuilder<InvoiceRecord> b)
    {
        b.ToTable("Invoices");
        b.HasKey(i => i.Id);
        b.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(50);
        b.Property(i => i.Status).IsRequired().HasMaxLength(30);
        b.Property(i => i.LinesJson).HasColumnType("TEXT");
        b.HasIndex(i => i.IdempotencyKey).IsUnique();
        b.HasIndex(i => new { i.StoreId, i.InvoiceNumber }).IsUnique();
    }
}
