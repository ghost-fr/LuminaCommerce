using Lumina.Application.Auth;
using Lumina.Application.Catalogue;
using Lumina.Application.Pos;
using Lumina.Application.Ports;
using Lumina.Application.Stock;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Catalogue;
using Lumina.Contracts.Pos;
using Lumina.Fiscal.HashChain;
using Lumina.Infrastructure.Persistence;
using Lumina.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lumina.Infrastructure;

/// <summary>
/// Single source of truth for wiring the backend into a DI container. Called
/// identically from both Lumina.Pos.UI and Lumina.BackOffice.UI's composition
/// roots (App.axaml.cs) so the two apps never drift on what's registered — if a
/// new backend service needs registering, add it here once, not in both apps.
///
/// IMPORTANT for whoever wires the nav shell (Grok): almost everything below is
/// registered AddScoped, which is the right lifetime for anything touching
/// LuminaDbContext (not thread-safe, one unit of work at a time). In a desktop
/// app that never calls IServiceProvider.CreateScope() itself, resolving
/// directly from the root provider effectively gives you one shared instance
/// per service for the whole app run — which is what you want for IAuthService
/// (one login session for the process lifetime). If a nav/scope pattern is later
/// introduced that calls CreateScope() per screen, IAuthService.CurrentSession
/// would silently reset per screen — worth keeping in mind before adding one.
///
/// NOTE — this file replaces the composition-root version. As of Phase 4,
/// IStockAvailabilityChecker is backed by the real append-only stock ledger
/// (LedgerBackedStockAvailabilityChecker), not the Phase 3 always-available
/// placeholder — every sale now actually checks and decrements real stock.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLuminaBackend(
        this IServiceCollection services, string connectionString, Guid tenantId)
    {
        services.AddDbContext<LuminaDbContext>(options => options.UseSqlite(connectionString));

        services.AddSingleton<ICurrentTenantProvider>(new ConfigCurrentTenantProvider(tenantId));

        // Repositories (Phase 1-4)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ITaxCategoryRepository, TaxCategoryRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IRegisterRepository, RegisterRepository>();
        services.AddScoped<IVeriFactuChainStore, VeriFactuChainStore>();
        services.AddScoped<IStockLedgerRepository, StockLedgerRepository>();

        // Fiscal — stateless, safe as singletons
        services.AddSingleton<HashChainService>();
        services.AddSingleton<QrPayloadBuilder>();
        services.AddScoped<IFiscalRecordGenerator, FiscalRecordGenerator>();

        // Application services
        services.AddScoped<PricingService>();
        services.AddScoped<IProductCatalogueService, ProductCatalogueService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<StockLedgerService>();
        services.AddScoped<IStockAvailabilityChecker, LedgerBackedStockAvailabilityChecker>();
        services.AddScoped<IPosSaleService, PosSaleService>();

        return services;
    }
}
