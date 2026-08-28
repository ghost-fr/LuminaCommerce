using Lumina.Domain.Catalogue;
using Lumina.Domain.Identity;
using Lumina.Domain.Sales;
using Lumina.Domain.Stock;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence;

/// <summary>
/// Phase 1-4 scope. Later phases add DbSets here as their aggregates land
/// (CommercialDocument, ...) — this context is the single write-model root per the
/// blueprint's infrastructure layer, not one context per module.
///
/// NOTE — this file replaces the Phase 1/2/3 version of LuminaDbContext.cs at the
/// same path. This overwrite is intentional (adds the Phase 4 StockMovements
/// DbSet on top of everything already there, nothing removed).
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

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public LuminaDbContext(DbContextOptions<LuminaDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LuminaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
