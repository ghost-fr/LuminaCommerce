using Lumina.Application.Ports;
using Lumina.Domain.Catalogue;

namespace Lumina.Application.Catalogue;

public record ResolvedPrice(decimal CurrentPrice, decimal? OriginalPrice, decimal VatRate, bool HasActivePromotion);

/// <summary>
/// Single source of truth for "what does this product cost right now." Both the
/// catalogue browsing contract and the POS cart/sale flow MUST go through this —
/// never read Product.BasePrice directly and display it, or a promotion silently
/// won't apply on one screen but will on another.
/// </summary>
public class PricingService
{
    private readonly IProductRepository _products;
    private readonly ITaxCategoryRepository _taxCategories;
    private readonly IPromotionRepository _promotions;

    public PricingService(
        IProductRepository products, ITaxCategoryRepository taxCategories, IPromotionRepository promotions)
    {
        _products = products;
        _taxCategories = taxCategories;
        _promotions = promotions;
    }

    public async Task<ResolvedPrice> ResolveAsync(Product product, DateTimeOffset atMoment, CancellationToken ct = default)
    {
        var taxCategory = await _taxCategories.FindByIdAsync(product.TaxCategoryId, ct)
            ?? throw new InvalidOperationException(
                $"Product {product.Id} references TaxCategory {product.TaxCategoryId} which does not exist. " +
                "This is a data integrity issue, not a user error — surface as a system error, not a pricing rejection.");

        var activePromotions = await _promotions.GetActiveForProductAsync(product.Id, atMoment, ct);

        if (activePromotions.Count == 0)
            return new ResolvedPrice(product.BasePrice, null, taxCategory.Rate, false);

        // Multiple simultaneous active promotions on one product isn't modeled as a
        // supported case yet (no stacking rules) — pick the single best discount for
        // the customer and flag this as a data-entry issue to fix, not silently stack.
        var best = activePromotions
            .Select(p => p.Apply(product.BasePrice))
            .Min();

        return new ResolvedPrice(best, product.BasePrice, taxCategory.Rate, true);
    }
}
