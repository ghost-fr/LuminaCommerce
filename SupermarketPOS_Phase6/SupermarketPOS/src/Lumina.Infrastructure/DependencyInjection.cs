using Lumina.Application.Auth;
using Lumina.Application.Cash;
using Lumina.Application.Catalogue;
using Lumina.Application.Commercial;
using Lumina.Application.Pos;
using Lumina.Application.Ports;
using Lumina.Application.Stock;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Cash;
using Lumina.Contracts.Catalogue;
using Lumina.Contracts.Commercial;
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
/// roots (App.axaml.cs).
///
/// NOTE — this file replaces the Phase 5 version. Phase 6 adds cash-drawer
/// repositories/services: IRegisterSessionService (register open/close, manual
/// cash in/out) and the internal CashDrawerService PosSaleService now depends
/// on for recording the cash portion of completed sales.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLuminaBackend(
        this IServiceCollection services, string connectionString, Guid tenantId)
    {
        services.AddDbContext<LuminaDbContext>(options => options.UseSqlite(connectionString));

        services.AddSingleton<ICurrentTenantProvider>(new ConfigCurrentTenantProvider(tenantId));

        // Repositories (Phase 1-6)
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
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IRegisterSessionRepository, RegisterSessionRepository>();
        services.AddScoped<ICashMovementRepository, CashMovementRepository>();

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
        services.AddScoped<CashDrawerService>();
        services.AddScoped<IPosSaleService, PosSaleService>();
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IRegisterSessionService, RegisterSessionService>();

        return services;
    }
}
