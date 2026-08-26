using Lumina.Domain.Catalogue;
using Lumina.Domain.Identity;
using Lumina.Domain.Sales;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence;

/// <summary>
/// Phase 1-3 scope. Later phases add DbSets here as their aggregates land (StockLedger,
/// CommercialDocument, ...) — this context is the single write-model root per the
/// blueprint's infrastructure layer, not one context per module.
///
/// NOTE — this file replaces the Phase 1 version of LuminaDbContext.cs at the same
/// path. If you're applying this zip on top of an already-merged Phase 1, this
/// overwrite is intentional (adds Phase 2/3 DbSets to the Phase 1 ones, nothing
/// removed).
/// </summary>
public class LuminaDbContext : DbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();

    public DbSet<TaxCategory> TaxCategories => Set<TaxCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Promotion> Promotions => Set<Promotion>();

    public DbSet<Register> Registers => Set<Register>();
    public DbSet<CartRecord> CartRecords => Set<CartRecord>();
    public DbSet<SaleRecord> SaleRecords => Set<SaleRecord>();
    public DbSet<VeriFactuChainRecord> VeriFactuChainRecords => Set<VeriFactuChainRecord>();

    public LuminaDbContext(DbContextOptions<LuminaDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LuminaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
