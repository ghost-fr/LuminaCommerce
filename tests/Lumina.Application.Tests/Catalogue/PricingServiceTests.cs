using Lumina.Application.Catalogue;
using Lumina.Application.Ports;
using Lumina.Domain.Catalogue;
using Xunit;

namespace Lumina.Application.Tests.Catalogue;

public class PricingServiceTests
{
    private sealed class FakeProductRepository : IProductRepository
    {
        public Task<Product?> FindByIdAsync(Guid productId, CancellationToken ct = default) => Task.FromResult<Product?>(null);
        public Task<Product?> FindByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct = default) => Task.FromResult<Product?>(null);
        public Task<(IReadOnlyList<Product>, int)> SearchAsync(Guid tenantId, string? query, int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Product>, int)>((Array.Empty<Product>(), 0));
    }

    private sealed class FakeTaxCategoryRepository : ITaxCategoryRepository
    {
        public TaxCategory? TaxCategory;
        public Task<TaxCategory?> FindByIdAsync(Guid taxCategoryId, CancellationToken ct = default) => Task.FromResult(TaxCategory);
    }

    private sealed class FakePromotionRepository : IPromotionRepository
    {
        public IReadOnlyList<Promotion> Active = Array.Empty<Promotion>();
        public Task<IReadOnlyList<Promotion>> GetActiveForProductAsync(Guid productId, DateTimeOffset atMoment, CancellationToken ct = default) =>
            Task.FromResult(Active);
    }

    [Fact]
    public async Task ResolveAsync_NoPromotion_ReturnsBasePrice()
    {
        var taxCategories = new FakeTaxCategoryRepository { TaxCategory = new TaxCategory(Guid.NewGuid(), Guid.NewGuid(), "General", 0.21m) };
        var promotions = new FakePromotionRepository();
        var svc = new PricingService(new FakeProductRepository(), taxCategories, promotions);
        var product = new Product(Guid.NewGuid(), Guid.NewGuid(), "123", "Widget", taxCategories.TaxCategory!.Id, 10.00m);

        var result = await svc.ResolveAsync(product, DateTimeOffset.UtcNow);

        Assert.Equal(10.00m, result.CurrentPrice);
        Assert.Null(result.OriginalPrice);
        Assert.False(result.HasActivePromotion);
        Assert.Equal(0.21m, result.VatRate);
    }

    [Fact]
    public async Task ResolveAsync_WithPercentagePromotion_AppliesDiscount()
    {
        var taxCategories = new FakeTaxCategoryRepository { TaxCategory = new TaxCategory(Guid.NewGuid(), Guid.NewGuid(), "General", 0.21m) };
        var product = new Product(Guid.NewGuid(), Guid.NewGuid(), "123", "Widget", taxCategories.TaxCategory!.Id, 10.00m);
        var promo = new Promotion(
            Guid.NewGuid(), Guid.NewGuid(), product.Id, "SUMMER10",
            PromotionDiscountType.Percentage, 0.10m,
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var promotions = new FakePromotionRepository { Active = new[] { promo } };
        var svc = new PricingService(new FakeProductRepository(), taxCategories, promotions);

        var result = await svc.ResolveAsync(product, DateTimeOffset.UtcNow);

        Assert.Equal(9.00m, result.CurrentPrice);
        Assert.Equal(10.00m, result.OriginalPrice);
        Assert.True(result.HasActivePromotion);
    }

    [Fact]
    public async Task ResolveAsync_MissingTaxCategory_Throws()
    {
        var taxCategories = new FakeTaxCategoryRepository { TaxCategory = null };
        var svc = new PricingService(new FakeProductRepository(), taxCategories, new FakePromotionRepository());
        var product = new Product(Guid.NewGuid(), Guid.NewGuid(), "123", "Widget", Guid.NewGuid(), 10.00m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ResolveAsync(product, DateTimeOffset.UtcNow));
    }
}
