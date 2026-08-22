using Lumina.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lumina.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("Tenants");
        b.HasKey(t => t.Id);
        b.Property(t => t.LegalName).IsRequired().HasMaxLength(200);
        b.Property(t => t.Nif).IsRequired().HasMaxLength(20);
        b.HasIndex(t => t.Nif).IsUnique();
        b.HasMany(t => t.Stores).WithOne().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> b)
    {
        b.ToTable("Stores");
        b.HasKey(s => s.Id);
        b.Property(s => s.Name).IsRequired().HasMaxLength(200);
        b.Property(s => s.TimeZoneId).IsRequired().HasMaxLength(64);
        b.HasIndex(s => s.TenantId);
    }
}
