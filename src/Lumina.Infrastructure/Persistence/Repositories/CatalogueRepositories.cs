using Lumina.Application.Ports;
using Lumina.Domain.Catalogue;
using Lumina.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly LuminaDbContext _db;
    public ProductRepository(LuminaDbContext db) => _db = db;

    public Task<Product?> FindByIdAsync(Guid productId, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);

    public Task<Product?> FindByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Barcode == barcode, ct);

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchAsync(
        Guid tenantId, string? query, int page, int pageSize, CancellationToken ct = default)
    {
        var baseQuery = _db.Products.Where(p => p.TenantId == tenantId && p.IsActive);
        if (!string.IsNullOrWhiteSpace(query))
            baseQuery = baseQuery.Where(p => p.Name.Contains(query) || p.Barcode.Contains(query));

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderBy(p => p.Name)
            .Skip(Math.Max(0, (page - 1) * pageSize))
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}

public class TaxCategoryRepository : ITaxCategoryRepository
{
    private readonly LuminaDbContext _db;
    public TaxCategoryRepository(LuminaDbContext db) => _db = db;

    public Task<TaxCategory?> FindByIdAsync(Guid taxCategoryId, CancellationToken ct = default) =>
        _db.TaxCategories.FirstOrDefaultAsync(t => t.Id == taxCategoryId, ct);
}

public class PromotionRepository : IPromotionRepository
{
    private readonly LuminaDbContext _db;
    public PromotionRepository(LuminaDbContext db) => _db = db;

    public async Task<IReadOnlyList<Promotion>> GetActiveForProductAsync(
        Guid productId, DateTimeOffset atMoment, CancellationToken ct = default)
    {
        return await _db.Promotions
            .Where(p => p.ProductId == productId && p.StartsAt <= atMoment && atMoment < p.EndsAt)
            .ToListAsync(ct);
    }
}

public class TenantRepository : ITenantRepository
{
    private readonly LuminaDbContext _db;
    public TenantRepository(LuminaDbContext db) => _db = db;

    public Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
}
