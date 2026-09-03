using Lumina.Domain.Cash;
using Lumina.Domain.Catalogue;
using Lumina.Domain.Identity;
using Lumina.Domain.Sales;
using Lumina.Domain.Stock;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence;

/// <summary>
/// Phase 1-8 scope plus the correctness-hardening pass (NumberSequences).
/// This context is the single write-model root per the blueprint's
/// infrastructure layer, not one context per module.
///
/// NOTE — this file replaces the Phase 8 version. Adds NumberSequences (backs
/// NumberSequenceGenerator's atomic ticket/invoice numbering) on top of
/// everything already there.
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

    public DbSet<QuoteRecord> QuoteRecords => Set<QuoteRecord>();
    public DbSet<InvoiceRecord> InvoiceRecords => Set<InvoiceRecord>();

    public DbSet<RegisterSession> RegisterSessions => Set<RegisterSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();

    public DbSet<NumberSequenceRecord> NumberSequences => Set<NumberSequenceRecord>();

    public LuminaDbContext(DbContextOptions<LuminaDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LuminaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
