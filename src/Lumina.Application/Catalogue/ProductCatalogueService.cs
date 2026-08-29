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
    private readonly ICurrentTenantProvider _tenant;

    public ProductCatalogueService(
        IProductRepository products,
        PricingService pricing,
        ICurrentTenantProvider tenant)
    {
        _products = products;
        _pricing = pricing;
        _tenant = tenant;
    }

    public async Task<ProductSearchResult> SearchAsync(
        ProductSearchRequest request, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 50 : Math.Min(request.PageSize, 200);

        var (items, total) = await _products.SearchAsync(
            tenantId, request.Query, page, pageSize, ct);

        var summaries = new List<IProductSummary>(items.Count);
        var now = DateTimeOffset.UtcNow;
        foreach (var product in items)
        {
            if (!product.IsActive) continue;
            var price = await _pricing.ResolveAsync(product, now, ct);
            summaries.Add(new ProductSummary
            {
                ProductId = product.Id,
                Barcode = product.Barcode,
                Name = product.Name,
                CurrentPrice = price.CurrentPrice,
                OriginalPrice = price.OriginalPrice,
                VatRate = price.VatRate,
                HasActivePromotion = price.HasActivePromotion
            });
        }

        return new ProductSearchResult(summaries, total, page, pageSize);
    }

    public Task<IProductSummary?> FindByBarcodeAsync(string barcode, CancellationToken ct = default) =>
        FindByBarcodeInternalAsync(_tenant.TenantId, barcode, ct);

    /// <summary>
    /// Used by PosSaleService with an explicit tenantId (same process, shared provider).
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
