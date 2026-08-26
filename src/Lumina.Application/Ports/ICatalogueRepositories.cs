using Lumina.Domain.Catalogue;

namespace Lumina.Application.Ports;

public interface IProductRepository
{
    Task<Product?> FindByIdAsync(Guid productId, CancellationToken ct = default);
    Task<Product?> FindByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct = default);
    Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        Guid tenantId, string? query, int page, int pageSize, CancellationToken ct = default);
}

public interface ITaxCategoryRepository
{
    Task<TaxCategory?> FindByIdAsync(Guid taxCategoryId, CancellationToken ct = default);
}

public interface IPromotionRepository
{
    /// <summary>Active promotions for a product at a given moment. Empty, not null,
    /// when none active — callers should not need a null check.</summary>
    Task<IReadOnlyList<Promotion>> GetActiveForProductAsync(
        Guid productId, DateTimeOffset atMoment, CancellationToken ct = default);
}
