using Lumina.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence;

/// <summary>
/// Phase 1 scope: Foundation entities only (Tenant, Store, User, Role). Later phases
/// add DbSets here as their aggregates land (Product, Sale, StockLedger, ...) — this
/// context is the single write-model root per the blueprint's infrastructure layer,
/// not one context per module.
/// </summary>
public class LuminaDbContext : DbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();

    public LuminaDbContext(DbContextOptions<LuminaDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LuminaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
