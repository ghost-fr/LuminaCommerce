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
/// </summary>
public record CompleteSaleRequest(
    Guid RegisterId,
    Guid? CustomerId,
    IReadOnlyList<TenderLine> Tenders);

public enum CompleteSaleStatus
{
    Success,
    /// <summary>Sale was NOT committed. UI must not print a ticket or clear the cart.</summary>
    Rejected,
    /// <summary>Sale WAS committed locally but VeriFactu submission is queued/retrying.
    /// UI proceeds as success — this is expected in VERI*FACTU async mode.</summary>
    SuccessPendingSubmission
}

public record CompleteSaleResult(
    CompleteSaleStatus Status,
    Guid? SaleId,
    string? TicketNumber,
    string? QrPayload,
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
