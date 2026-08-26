using Lumina.Application.Ports;
using Lumina.Contracts.Catalogue;

namespace Lumina.Application.Catalogue;

internal sealed class ProductSummary : IProductSummary
{
    public Guid ProductId { get; init; }
    public string Barcode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal CurrentPrice { get; init; }
    public decimal? OriginalPrice { get; init; }
    public decimal VatRate { get; init; }
    public bool HasActivePromotion { get; init; }
}

public sealed class ProductCatalogueService : IProductCatalogueService
{
    private readonly IProductRepository _products;
    private readonly PricingService _pricing;

    public ProductCatalogueService(IProductRepository products, PricingService pricing)
    {
        _products = products;
        _pricing = pricing;
    }

    public Task<ProductSearchResult> SearchAsync(ProductSearchRequest request, CancellationToken ct = default)
    {
        // NOTE: tenant scoping omitted here pending the same ICurrentTenantProvider
        // wiring used by AuthService — this signature will take/derive TenantId once
        // the composition root passes it through. Flagged, not silently assumed.
        throw new NotImplementedException(
            "SearchAsync requires ICurrentTenantProvider wiring at the composition root " +
            "(same pattern as AuthService) — implement alongside App.axaml.cs DI setup.");
    }

    public Task<IProductSummary?> FindByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        throw new NotImplementedException(
            "FindByBarcodeAsync requires ICurrentTenantProvider wiring — see SearchAsync note.");
    }

    /// <summary>
    /// Internal helper actually exercised by tests and by PosSaleService — takes an
    /// explicit tenantId until the composition-root wiring above lands, so pricing
    /// logic itself is fully testable now rather than blocked on DI plumbing.
    /// </summary>
    internal async Task<IProductSummary?> FindByBarcodeInternalAsync(
        Guid tenantId, string barcode, CancellationToken ct = default)
    {
        var product = await _products.FindByBarcodeAsync(tenantId, barcode, ct);
        if (product is null || !product.IsActive) return null;

        var price = await _pricing.ResolveAsync(product, DateTimeOffset.UtcNow, ct);
        return new ProductSummary
        {
            ProductId = product.Id,
            Barcode = product.Barcode,
            Name = product.Name,
            CurrentPrice = price.CurrentPrice,
            OriginalPrice = price.OriginalPrice,
            VatRate = price.VatRate,
            HasActivePromotion = price.HasActivePromotion
        };
    }
}
