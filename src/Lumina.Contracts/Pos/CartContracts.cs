namespace Lumina.Contracts.Pos;

/// <summary>
/// Read-only view of a single cart line, bound to by the POS UI.
/// Backend (Application layer) produces instances of a concrete type
/// implementing this; UI never constructs one directly.
/// </summary>
public interface ICartLine
{
    Guid ProductId { get; }
    string ProductName { get; }
    string Barcode { get; }
    decimal UnitPrice { get; }
    decimal Quantity { get; }
    decimal VatRate { get; }
    decimal LineTotal { get; }
    string? AppliedPromotionCode { get; }
}

/// <summary>
/// Read-only view of the full cart, bound to by the POS UI cart screen.
/// </summary>
public interface ICartSummary
{
    IReadOnlyList<ICartLine> Lines { get; }
    decimal Subtotal { get; }
    decimal VatTotal { get; }
    decimal Total { get; }
    Guid? CustomerId { get; }
}
