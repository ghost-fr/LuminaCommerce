namespace Lumina.Domain.Sales;

public record SaleLine(
    Guid ProductId, decimal Quantity, decimal UnitPrice, decimal VatRate,
    decimal LineSubtotal, decimal LineVat, decimal LineTotal, string? PromotionCode);

public record SaleTender(string TenderType, decimal Amount, string? Reference);

/// <summary>
/// Immutable once constructed — a completed sale is never mutated. A correction is
/// always a new, linked document (rectificativa), never an edit to this aggregate.
/// This is a hard fiscal requirement, not a style preference: enforce it by never
/// adding a setter here, ever, even for a "just this once" fix.
/// </summary>
public class Sale
{
    public Guid Id { get; }
    public Guid StoreId { get; }
    public Guid RegisterId { get; }
    public Guid? CustomerId { get; }
    public string TicketNumber { get; }
    public IReadOnlyList<SaleLine> Lines { get; }
    public IReadOnlyList<SaleTender> Tenders { get; }
    public decimal Subtotal { get; }
    public decimal VatTotal { get; }
    public decimal Total { get; }
    public DateTimeOffset CompletedAt { get; }
    public Guid IdempotencyKey { get; }

    /// <summary>Set after Lumina.Fiscal generates the hash-chained VeriFactu record.
    /// Sale can exist transiently without this during construction but MUST have it
    /// before the transaction commits — enforced by the caller (PosSaleService),
    /// not by this constructor, since Fiscal must not be referenced from Domain
    /// (dependency direction: Fiscal -> Domain, never the reverse).</summary>
    public Guid? VeriFactuRecordId { get; private set; }

    public Sale(
        Guid id, Guid storeId, Guid registerId, Guid? customerId, string ticketNumber,
        IReadOnlyList<SaleLine> lines, IReadOnlyList<SaleTender> tenders, Guid idempotencyKey)
        : this(id, storeId, registerId, customerId, ticketNumber, lines, tenders, idempotencyKey,
               completedAt: null, veriFactuRecordId: null)
    {
    }

    /// <summary>
    /// Full constructor used both for creating a brand-new sale (completedAt: null
    /// stamps "now") and for rehydrating a persisted sale (completedAt: the original
    /// timestamp). Not exposed as "Reconstruct" or similar to keep this simple, but
    /// callers rehydrating from storage MUST pass the original CompletedAt — passing
    /// null there would silently rewrite history to "now," which is exactly the bug
    /// this overload exists to prevent.
    /// </summary>
    public Sale(
        Guid id, Guid storeId, Guid registerId, Guid? customerId, string ticketNumber,
        IReadOnlyList<SaleLine> lines, IReadOnlyList<SaleTender> tenders, Guid idempotencyKey,
        DateTimeOffset? completedAt, Guid? veriFactuRecordId)
    {
        if (lines.Count == 0)
            throw new ArgumentException("A sale must have at least one line.", nameof(lines));
        if (tenders.Count == 0)
            throw new ArgumentException("A sale must have at least one tender.", nameof(tenders));

        Id = id;
        StoreId = storeId;
        RegisterId = registerId;
        CustomerId = customerId;
        TicketNumber = ticketNumber;
        Lines = lines;
        Tenders = tenders;
        IdempotencyKey = idempotencyKey;

        Subtotal = lines.Sum(l => l.LineSubtotal);
        VatTotal = lines.Sum(l => l.LineVat);
        Total = Subtotal + VatTotal;

        var tenderTotal = tenders.Sum(t => t.Amount);
        if (tenderTotal != Total)
            throw new InvalidOperationException(
                $"Tender total ({tenderTotal}) does not match sale total ({Total}). " +
                "This must be validated and rejected BEFORE constructing a Sale, not caught here — " +
                "if this throws, it's a bug in the caller's validation, not a normal rejection path.");

        CompletedAt = completedAt ?? DateTimeOffset.UtcNow;
        VeriFactuRecordId = veriFactuRecordId;
    }

    public void AttachVeriFactuRecord(Guid veriFactuRecordId)
    {
        if (VeriFactuRecordId is not null)
            throw new InvalidOperationException("A VeriFactu record is already attached to this sale.");
        VeriFactuRecordId = veriFactuRecordId;
    }
}
