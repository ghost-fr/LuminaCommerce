using Lumina.Application.Auth;
using Lumina.Application.Catalogue;
using Lumina.Application.Commercial;
using Lumina.Application.Pos;
using Lumina.Application.Ports;
using Lumina.Application.Stock;
using Lumina.Contracts.Auth;
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
/// NOTE — this file replaces the Phase 4 version. Phase 5 adds Quote/Invoice
/// repositories and services (QuoteService, InvoiceService). QuoteService and
/// InvoiceService are the natural home for the Commercial-documents UI screens
/// (quote builder, invoice list) — likely a Lumina.BackOffice.UI concern more
/// than Lumina.Pos.UI, but both apps get the same registrations for now; split
/// later if the two apps' DI needs genuinely diverge.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLuminaBackend(
        this IServiceCollection services, string connectionString, Guid tenantId)
    {
        services.AddDbContext<LuminaDbContext>(options => options.UseSqlite(connectionString));

        services.AddSingleton<ICurrentTenantProvider>(new ConfigCurrentTenantProvider(tenantId));

        // Repositories (Phase 1-5)
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
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<IInvoiceService, InvoiceService>();

        return services;
    }
}
