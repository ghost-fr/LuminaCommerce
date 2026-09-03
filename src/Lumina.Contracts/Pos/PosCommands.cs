namespace Lumina.Contracts.Pos;

public enum TenderType
{
    Cash,
    Card,
    MixedCashCard,
    Other
}

public record TenderLine(TenderType Type, decimal Amount, string? Reference = null);

/// <summary>Add or increment a product line by scanning/typing a barcode.</summary>
public record AddLineRequest(string Barcode, decimal Quantity);

/// <summary>Remove or reduce quantity of an existing line.</summary>
public record RemoveLineRequest(Guid ProductId, decimal Quantity);

/// <summary>
/// Finalize the sale. Fiscal-critical boundary — see CONTRACTS.md.
/// </summary>
public record CompleteSaleRequest(
    Guid RegisterId,
    Guid? CustomerId,
    IReadOnlyList<TenderLine> Tenders,
    Guid IdempotencyKey);

public enum CompleteSaleStatus
{
    Success,
    Rejected,
    SuccessPendingSubmission
}

public enum RejectionCode
{
    None,
    StockUnavailable,
    PaymentValidationFailed,
    RegisterNotOpen,
    PriceChanged,
    CustomerRequired,
    DuplicateSubmission,
    FiscalChainError,
    Unknown
}

public record CompleteSaleResult(
    CompleteSaleStatus Status,
    Guid? SaleId,
    string? TicketNumber,
    string? QrPayload,
    RejectionCode RejectionCode,
    string? RejectionReason);

/// <summary>
/// UI-facing contract for the POS sale workflow.
/// </summary>
public interface IPosSaleService
{
    /// <summary>Create a new empty cart for the store. Returns cart id.</summary>
    Task<Guid> CreateCartAsync(Guid storeId, Guid? customerId = null, CancellationToken ct = default);

    /// <summary>First open register for the store, or null if none open.</summary>
    Task<Guid?> GetOpenRegisterIdAsync(Guid storeId, CancellationToken ct = default);

    Task<ICartSummary> AddLineAsync(Guid cartId, AddLineRequest request, CancellationToken ct = default);
    Task<ICartSummary> RemoveLineAsync(Guid cartId, RemoveLineRequest request, CancellationToken ct = default);
    Task<CompleteSaleResult> CompleteSaleAsync(Guid cartId, CompleteSaleRequest request, CancellationToken ct = default);
}
