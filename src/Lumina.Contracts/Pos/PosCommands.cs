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
/// Finalize the sale. This is the fiscal-critical boundary: on success the backend
/// has already created the immutable Sale aggregate, emitted stock events, generated
/// the VeriFactu hash-chained record, and produced a QR payload — none of that is
/// re-derivable from the UI, so the UI must treat CompleteSaleResult as authoritative.
///
/// IdempotencyKey: UI generates a fresh Guid the first time the operator submits
/// tenders, and MUST resend the SAME key on any retry of that same attempt (e.g.
/// after a timeout or connection drop). Backend uses this to detect and safely
/// no-op a duplicate submission instead of creating a second Sale. UI must NOT
/// generate a new key on retry, and MUST generate a new key for a genuinely new
/// sale (new cart, or same cart resubmitted after a Rejected result and operator
/// changes).
/// </summary>
public record CompleteSaleRequest(
    Guid RegisterId,
    Guid? CustomerId,
    IReadOnlyList<TenderLine> Tenders,
    Guid IdempotencyKey);

public enum CompleteSaleStatus
{
    Success,
    /// <summary>Sale was NOT committed. UI must not print a ticket or clear the cart.</summary>
    Rejected,
    /// <summary>Sale WAS committed locally but VeriFactu submission is queued/retrying.
    /// UI proceeds as success — this is expected in VERI*FACTU async mode.</summary>
    SuccessPendingSubmission
}

/// <summary>
/// Machine-readable rejection taxonomy so UI can localize/style messages instead of
/// displaying raw backend strings. RejectionReason (free text) remains for logs/
/// operator detail; UI should switch on RejectionCode for the primary message and
/// only show RejectionReason as secondary detail (e.g. an expandable "details" line).
/// </summary>
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
/// UI-facing contract for the POS sale workflow. Concrete implementation lives in
/// Lumina.Application and is resolved via DI; the UI project only ever depends on
/// this interface, never on the implementation or on Infrastructure directly.
/// </summary>
public interface IPosSaleService
{
    Task<ICartSummary> AddLineAsync(Guid cartId, AddLineRequest request, CancellationToken ct = default);
    Task<ICartSummary> RemoveLineAsync(Guid cartId, RemoveLineRequest request, CancellationToken ct = default);
    Task<CompleteSaleResult> CompleteSaleAsync(Guid cartId, CompleteSaleRequest request, CancellationToken ct = default);
}
