namespace Lumina.Domain.Sales;

/// <summary>
/// A physical/logical checkout terminal within a Store. Must be open before a sale
/// can complete against it — this is the "RegisterNotOpen" rejection path.
/// </summary>
public class Register
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string Name { get; private set; }
    public bool IsOpen { get; private set; }
    public DateTimeOffset? OpenedAt { get; private set; }

    private Register() { Name = string.Empty; }

    public Register(Guid id, Guid storeId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Register name is required.", nameof(name));
        Id = id;
        StoreId = storeId;
        Name = name;
        IsOpen = false;
    }

    public void Open()
    {
        IsOpen = true;
        OpenedAt = DateTimeOffset.UtcNow;
    }

    public void Close()
    {
        IsOpen = false;
        OpenedAt = null;
    }
}

/// <summary>
/// Working, mutable cart state — not yet fiscal. Becomes an immutable Sale only via
/// CompleteSale. Cart itself never touches VeriFactu/hash-chain concerns.
/// </summary>
public class Cart
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid? CustomerId { get; private set; }

    private readonly List<CartLine> _lines = new();
    public IReadOnlyList<CartLine> Lines => _lines;

    private Cart() { }

    public Cart(Guid id, Guid storeId)
    {
        Id = id;
        StoreId = storeId;
    }

    public void SetCustomer(Guid? customerId) => CustomerId = customerId;

    /// <summary>Adds a new line or increments an existing one for the same product +
    /// same price/promo snapshot. A price change mid-cart (rare, but possible if a
    /// promotion expires between scans) intentionally creates a separate line rather
    /// than silently merging at a stale price.</summary>
    public CartLine AddLine(Guid productId, decimal quantity, decimal unitPrice, decimal vatRate, string? promotionCode)
    {
        if (quantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        var existing = _lines.FirstOrDefault(l =>
            l.ProductId == productId && l.UnitPrice == unitPrice && l.PromotionCode == promotionCode);

        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
            return existing;
        }

        var line = new CartLine(productId, quantity, unitPrice, vatRate, promotionCode);
        _lines.Add(line);
        return line;
    }

    public void RemoveQuantity(Guid productId, decimal quantity)
    {
        var line = _lines.FirstOrDefault(l => l.ProductId == productId)
            ?? throw new InvalidOperationException($"No cart line for product {productId}.");

        line.DecreaseQuantity(quantity);
        if (line.Quantity <= 0m)
            _lines.Remove(line);
    }

    public decimal Subtotal => _lines.Sum(l => l.LineSubtotal);
    public decimal VatTotal => _lines.Sum(l => l.LineVat);
    public decimal Total => Subtotal + VatTotal;
}

public class CartLine
{
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal VatRate { get; private set; }
    public string? PromotionCode { get; private set; }

    internal CartLine(Guid productId, decimal quantity, decimal unitPrice, decimal vatRate, string? promotionCode)
    {
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatRate = vatRate;
        PromotionCode = promotionCode;
    }

    internal void IncreaseQuantity(decimal amount) => Quantity += amount;

    internal void DecreaseQuantity(decimal amount)
    {
        if (amount > Quantity)
            throw new InvalidOperationException("Cannot remove more than current line quantity.");
        Quantity -= amount;
    }

    // Line totals computed on unit price EXCLUDING vat, VAT added on top — consistent
    // with blueprint's price-storage convention. If BasePrice is ever stored
    // VAT-inclusive instead, this must change everywhere at once (see CONTRACTS.md
    // §1 guarantee: "display what's given," so backend's convention is what UI shows).
    public decimal LineSubtotal => Math.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
    public decimal LineVat => Math.Round(LineSubtotal * VatRate, 2, MidpointRounding.AwayFromZero);
    public decimal LineTotal => LineSubtotal + LineVat;
}
