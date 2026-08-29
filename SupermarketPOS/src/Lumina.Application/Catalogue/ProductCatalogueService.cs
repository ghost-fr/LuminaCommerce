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
    private readonly ICurrentTenantProvider _tenantProvider;

    public ProductCatalogueService(
        IProductRepository products, PricingService pricing, ICurrentTenantProvider tenantProvider)
    {
        _products = products;
        _pricing = pricing;
        _tenantProvider = tenantProvider;
    }

    public async Task<ProductSearchResult> SearchAsync(ProductSearchRequest request, CancellationToken ct = default)
    {
        var (items, total) = await _products.SearchAsync(
            _tenantProvider.TenantId, request.Query, request.Page, request.PageSize, ct);

        var summaries = new List<IProductSummary>();
        foreach (var product in items)
            summaries.Add(await ToSummaryAsync(product, ct));

        return new ProductSearchResult(summaries, total, request.Page, request.PageSize);
    }

    public async Task<IProductSummary?> FindByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var product = await _products.FindByBarcodeAsync(_tenantProvider.TenantId, barcode, ct);
        if (product is null || !product.IsActive) return null;
        return await ToSummaryAsync(product, ct);
    }

    private async Task<IProductSummary> ToSummaryAsync(Domain.Catalogue.Product product, CancellationToken ct)
    {
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
