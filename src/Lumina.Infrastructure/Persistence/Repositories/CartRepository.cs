using System.Text.Json;
using Lumina.Application.Ports;
using Lumina.Domain.Sales;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

public class CartRepository : ICartRepository
{
    private readonly LuminaDbContext _db;
    public CartRepository(LuminaDbContext db) => _db = db;

    public async Task<Cart?> FindByIdAsync(Guid cartId, CancellationToken ct = default)
    {
        var record = await _db.CartRecords.FirstOrDefaultAsync(c => c.Id == cartId, ct);
        return record is null ? null : ToDomain(record);
    }

    public async Task AddAsync(Cart cart, CancellationToken ct = default)
    {
        await _db.CartRecords.AddAsync(ToRecord(cart), ct);
    }

    public async Task UpdateAsync(Cart cart, CancellationToken ct = default)
    {
        var record = await _db.CartRecords.FirstOrDefaultAsync(c => c.Id == cart.Id, ct)
            ?? throw new InvalidOperationException($"Cannot update Cart {cart.Id}: no persisted record found. Call AddAsync first.");

        record.CustomerId = cart.CustomerId;
        record.LinesJson = SerializeLines(cart);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    private static string SerializeLines(Cart cart) =>
        JsonSerializer.Serialize(cart.Lines.Select(l =>
            new CartLineJson(l.ProductId, l.Quantity, l.UnitPrice, l.VatRate, l.PromotionCode)));

    private static CartRecord ToRecord(Cart cart) => new()
    {
        Id = cart.Id,
        StoreId = cart.StoreId,
        CustomerId = cart.CustomerId,
        LinesJson = SerializeLines(cart)
    };

    private static Cart ToDomain(CartRecord record)
    {
        var cart = new Cart(record.Id, record.StoreId);
        cart.SetCustomer(record.CustomerId);

        var lines = JsonSerializer.Deserialize<List<CartLineJson>>(record.LinesJson) ?? new();
        foreach (var line in lines)
            cart.AddLine(line.ProductId, line.Quantity, line.UnitPrice, line.VatRate, line.PromotionCode);

        return cart;
    }
}
