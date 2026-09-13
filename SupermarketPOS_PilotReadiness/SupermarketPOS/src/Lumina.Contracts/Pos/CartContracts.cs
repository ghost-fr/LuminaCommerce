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
///
/// UPDATED: gained CartId. This was a real, blocking gap — without it, the
/// UI had no way to reference the cart it just created (there was also no
/// CreateCartAsync at all until now, see IPosSaleService.CreateCartAsync)
/// or, after the fact, no way to confirm which cart an AddLineAsync/
/// RemoveLineAsync response actually belongs to. Additive change — existing
/// bindings against this interface still compile.
/// </summary>
public interface ICartSummary
{
    Guid CartId { get; }
    IReadOnlyList<ICartLine> Lines { get; }
    decimal Subtotal { get; }
    decimal VatTotal { get; }
    decimal Total { get; }
    Guid? CustomerId { get; }
}
