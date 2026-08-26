using Lumina.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumina.Infrastructure.Persistence.Configurations;

public class TaxCategoryConfiguration : IEntityTypeConfiguration<TaxCategory>
{
    public void Configure(EntityTypeBuilder<TaxCategory> b)
    {
        b.ToTable("TaxCategories");
        b.HasKey(t => t.Id);
        b.Property(t => t.Name).IsRequired().HasMaxLength(100);
        b.Property(t => t.Rate).HasColumnType("decimal(5,4)");
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");
        b.HasKey(p => p.Id);
        b.Property(p => p.Barcode).IsRequired().HasMaxLength(64);
        b.Property(p => p.Name).IsRequired().HasMaxLength(300);
        b.Property(p => p.BasePrice).HasColumnType("decimal(12,2)");
        b.HasIndex(p => new { p.TenantId, p.Barcode }).IsUnique();
    }
}

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> b)
    {
        b.ToTable("Promotions");
        b.HasKey(p => p.Id);
        b.Property(p => p.Code).IsRequired().HasMaxLength(64);
        b.Property(p => p.Value).HasColumnType("decimal(12,4)");
        b.HasIndex(p => p.ProductId);
        b.HasIndex(p => new { p.StartsAt, p.EndsAt });
    }
}
