namespace Lumina.Domain.Catalogue;

/// <summary>
/// Spanish VAT category (IVA general 21%, reducido 10%, superreducido 4%, exento 0%,
/// plus IGIC/IPSI for Canarias/Ceuta-Melilla if ever needed — out of scope for now,
/// flagged as a follow-up rather than silently unsupported).
/// </summary>
public class TaxCategory
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public decimal Rate { get; private set; } // e.g. 0.21m for 21%

    private TaxCategory() { Name = string.Empty; }

    public TaxCategory(Guid id, Guid tenantId, string name, decimal rate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tax category name is required.", nameof(name));
        if (rate < 0m || rate > 1m)
            throw new ArgumentOutOfRangeException(nameof(rate), "VAT rate must be between 0 and 1 (e.g. 0.21 for 21%).");

        Id = id;
        TenantId = tenantId;
        Name = name;
        Rate = rate;
    }
}

/// <summary>
/// A sellable product. BasePrice is the list price before any active promotion is
/// applied — PricingService (Application layer) resolves the effective price at
/// sale time, this entity never computes a "current" price itself.
/// </summary>
public class Product
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Barcode { get; private set; }
    public string Name { get; private set; }
    public Guid TaxCategoryId { get; private set; }
    public decimal BasePrice { get; private set; }
    public bool IsActive { get; private set; }

    private Product() { Barcode = string.Empty; Name = string.Empty; }

    public Product(Guid id, Guid tenantId, string barcode, string name, Guid taxCategoryId, decimal basePrice)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            throw new ArgumentException("Barcode is required.", nameof(barcode));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));
        if (basePrice < 0m)
            throw new ArgumentOutOfRangeException(nameof(basePrice), "Base price cannot be negative.");

        Id = id;
        TenantId = tenantId;
        Barcode = barcode;
        Name = name;
        TaxCategoryId = taxCategoryId;
        BasePrice = basePrice;
        IsActive = true;
    }

    public void Reprice(decimal newBasePrice)
    {
        if (newBasePrice < 0m)
            throw new ArgumentOutOfRangeException(nameof(newBasePrice), "Base price cannot be negative.");
        BasePrice = newBasePrice;
    }

    public void Deactivate() => IsActive = false;
}

/// <summary>
/// Simple percentage or fixed-amount discount on a single product, active within a
/// date window. Deliberately minimal — no bundle/basket-level promotions, no stacking
/// rules yet. Extend this (or add a sibling type) when the product needs those; don't
/// overload this type's meaning to cover cases it wasn't designed for.
/// </summary>
public class Promotion
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Code { get; private set; }
    public PromotionDiscountType DiscountType { get; private set; }
    public decimal Value { get; private set; } // percentage as 0..1, or fixed currency amount
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }

    private Promotion() { Code = string.Empty; }

    public Promotion(
        Guid id, Guid tenantId, Guid productId, string code,
        PromotionDiscountType discountType, decimal value,
        DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Promotion code is required.", nameof(code));
        if (endsAt <= startsAt)
            throw new ArgumentException("EndsAt must be after StartsAt.", nameof(endsAt));
        if (discountType == PromotionDiscountType.Percentage && (value <= 0m || value > 1m))
            throw new ArgumentOutOfRangeException(nameof(value), "Percentage discount must be between 0 and 1 (exclusive/inclusive).");
        if (discountType == PromotionDiscountType.FixedAmount && value <= 0m)
            throw new ArgumentOutOfRangeException(nameof(value), "Fixed discount amount must be positive.");

        Id = id;
        TenantId = tenantId;
        ProductId = productId;
        Code = code;
        DiscountType = discountType;
        Value = value;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public bool IsActiveAt(DateTimeOffset moment) => moment >= StartsAt && moment < EndsAt;

    public decimal Apply(decimal price) => DiscountType switch
    {
        PromotionDiscountType.Percentage => Math.Round(price * (1 - Value), 2, MidpointRounding.AwayFromZero),
        PromotionDiscountType.FixedAmount => Math.Max(0m, price - Value),
        _ => price
    };
}

public enum PromotionDiscountType
{
    Percentage,
    FixedAmount
}
