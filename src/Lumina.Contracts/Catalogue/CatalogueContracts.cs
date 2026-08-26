namespace Lumina.Contracts.Catalogue;

/// <summary>
/// Read-only product projection the UI browses/searches. Deliberately flatter than
/// the Product domain aggregate — no TaxCategoryId, just the resolved VatRate, so
/// the UI never has to join catalogue data itself.
/// </summary>
public interface IProductSummary
{
    Guid ProductId { get; }
    string Barcode { get; }
    string Name { get; }
    decimal CurrentPrice { get; }   // after any active promotion
    decimal? OriginalPrice { get; } // set only when CurrentPrice reflects a promotion; null otherwise
    decimal VatRate { get; }
    bool HasActivePromotion { get; }
}

public record ProductSearchRequest(string? Query, int Page = 1, int PageSize = 50);
public record ProductSearchResult(IReadOnlyList<IProductSummary> Items, int TotalCount, int Page, int PageSize);

/// <summary>
/// Catalogue browsing/lookup contract. UI (Grok) depends only on this; concrete
/// implementation lives in Lumina.Application.Catalogue.
/// </summary>
public interface IProductCatalogueService
{
    Task<ProductSearchResult> SearchAsync(ProductSearchRequest request, CancellationToken ct = default);

    /// <summary>Used by the POS barcode-scan flow — exact match, no fuzzy search.
    /// Returns null if not found or inactive; UI shows a "product not found" state,
    /// never a blank/zero-priced line.</summary>
    Task<IProductSummary?> FindByBarcodeAsync(string barcode, CancellationToken ct = default);
}
