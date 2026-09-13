using Lumina.Application.Auth;
using Lumina.Application.Catalogue;
using Lumina.Application.Devices;
using Lumina.Application.Pos;
using Lumina.Application.Ports;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Catalogue;
using Lumina.Contracts.Devices;
using Lumina.Contracts.Pos;
using Lumina.Contracts.Stock;
using Lumina.Fiscal.HashChain;
using Lumina.Infrastructure.Persistence;
using Lumina.Infrastructure.Persistence.Repositories;
using Lumina.Infrastructure.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lumina.Infrastructure;

/// <summary>
/// Single source of truth for wiring the backend into a DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLuminaBackend(
        this IServiceCollection services, string connectionString, Guid tenantId)
    {
        services.AddDbContext<LuminaDbContext>(options => options.UseSqlite(connectionString));

        services.AddSingleton<ICurrentTenantProvider>(new ConfigCurrentTenantProvider(tenantId));

        // Repositories (Phase 1-3)
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

        // Phase 3 placeholder stock check for sales; Phase 4 UI uses IStockService stub
        services.AddScoped<IStockAvailabilityChecker, PlaceholderStockAvailabilityChecker>();
        services.AddScoped<IStockService, StubStockService>();

        // Fiscal — stateless, safe as singletons
        services.AddSingleton<HashChainService>();
        services.AddSingleton<QrPayloadBuilder>();
        services.AddScoped<IFiscalRecordGenerator, FiscalRecordGenerator>();

        // Application services
        services.AddScoped<PricingService>();

        services.AddScoped<ProductCatalogueService>();
        services.AddScoped<IProductCatalogueService>(sp =>
            sp.GetRequiredService<ProductCatalogueService>());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPosSaleService, PosSaleService>();

        // Phase 8 devices — EMULATED. Swap only these two for real hardware.
        services.AddSingleton<IReceiptPrinter, SimulatedReceiptPrinter>();
        services.AddSingleton<IScaleService, SimulatedScaleService>();

        return services;
    }
}
