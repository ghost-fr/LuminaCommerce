namespace Lumina.Contracts.Commercial;

public interface IQuoteLine
{
    Guid ProductId { get; }
    string Description { get; }
    decimal Quantity { get; }
    decimal UnitPrice { get; }
    decimal VatRate { get; }
    decimal LineTotal { get; }
}

public interface IQuoteSummary
{
    Guid QuoteId { get; }
    Guid CustomerId { get; }
    string Status { get; } // string, not the domain enum — UI shouldn't depend on Domain types, same pattern as everywhere else in Contracts
    IReadOnlyList<IQuoteLine> Lines { get; }
    decimal Subtotal { get; }
    decimal VatTotal { get; }
    decimal Total { get; }
    DateTimeOffset ValidUntil { get; }
    Guid? ConvertedInvoiceId { get; }
}

public record CreateQuoteRequest(Guid StoreId, Guid CustomerId, DateTimeOffset ValidUntil);
public record AddQuoteLineRequest(Guid ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal VatRate);
public record RemoveQuoteLineRequest(Guid ProductId);

public enum ConvertToInvoiceStatus
{
    Success,
    /// <summary>Quote wasn't Accepted, or another precondition failed — nothing
    /// was created. UI shows RejectionReason, does not treat this as a partial
    /// success.</summary>
    Rejected
}

public record ConvertToInvoiceResult(
    ConvertToInvoiceStatus Status, Guid? InvoiceId, string? InvoiceNumber, string? QrPayload, string? RejectionReason);

/// <summary>
/// UI-facing contract for the quote lifecycle. Concrete implementation is
/// Lumina.Application.Commercial.QuoteService.
///
/// NOTE on CreateQuoteRequest.StoreId: passed explicitly rather than resolved
/// via session/tenant context, because Quote (like Cart) has no prior aggregate
/// to inherit a StoreId from at creation time. UI should populate this from
/// IUserSession.StoreId (Contracts.Auth) — same source Cart creation would use,
/// if/when Cart creation is added to a contract (currently no
/// IPosSaleService.CreateCart exists either; Cart creation isn't part of any
/// frozen contract yet, a pre-existing gap this note is surfacing, not
/// introducing).
///
/// UI (Grok) may assume:
/// - Only a Draft quote accepts AddLineAsync/RemoveLineAsync — calling either on
///   a Sent/Accepted/etc. quote throws; UI should disable line editing once a
///   quote leaves Draft, not rely on catching the exception as normal flow.
/// - ConvertToInvoiceAsync uses the SAME idempotency-key discipline as
///   IPosSaleService.CompleteSaleAsync (CONTRACTS.md §2.2) — generate one key per
///   submission attempt, reuse on retry, never invent InvoiceNumber/QrPayload
///   client-side.
/// - Like IPosSaleService, a retried ConvertToInvoiceAsync call currently returns
///   QrPayload: null on replay (same known gap as Sale — see PHASE2_3_NOTES.md
///   item 8, not re-solved here).
/// </summary>
public interface IQuoteService
{
    Task<IQuoteSummary> CreateAsync(CreateQuoteRequest request, CancellationToken ct = default);
    Task<IQuoteSummary> AddLineAsync(Guid quoteId, AddQuoteLineRequest request, CancellationToken ct = default);
    Task<IQuoteSummary> RemoveLineAsync(Guid quoteId, RemoveQuoteLineRequest request, CancellationToken ct = default);
    Task<IQuoteSummary> SendAsync(Guid quoteId, CancellationToken ct = default);
    Task<IQuoteSummary> AcceptAsync(Guid quoteId, CancellationToken ct = default);
    Task<IQuoteSummary> RejectAsync(Guid quoteId, CancellationToken ct = default);
    Task<ConvertToInvoiceResult> ConvertToInvoiceAsync(Guid quoteId, Guid idempotencyKey, CancellationToken ct = default);
}

public interface IInvoiceSummary
{
    Guid InvoiceId { get; }
    string InvoiceNumber { get; }
    string Status { get; }
    Guid CustomerId { get; }
    decimal Total { get; }
}

public enum CancelInvoiceStatus
{
    Success,
    Rejected
}

public record CancelInvoiceResult(CancelInvoiceStatus Status, string? RejectionReason);

/// <summary>
/// UI-facing contract for invoice lookup and cancellation. Concrete
/// implementation is Lumina.Application.Commercial.InvoiceService.
///
/// UI (Grok) may assume:
/// - CancelAsync requires a non-empty reason — this becomes part of the fiscal
///   record; the UI should require the operator to type one, not send an empty
///   or placeholder string.
/// - Cancelling is itself a real, separate fiscal submission (a new VeriFactu
///   "anulación" record, not a local-only status flip) — UI should treat it with
///   the same seriousness as completing a sale, not as a quick undo.
/// </summary>
public interface IInvoiceService
{
    Task<IInvoiceSummary?> FindByIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task<CancelInvoiceResult> CancelAsync(Guid invoiceId, string reason, CancellationToken ct = default);
}
