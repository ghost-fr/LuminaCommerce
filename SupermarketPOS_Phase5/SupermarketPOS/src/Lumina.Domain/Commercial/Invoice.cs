namespace Lumina.Domain.Commercial;

public enum InvoiceStatus
{
    Issued,
    Cancelled,
    Rectified
}

public record InvoiceLine(
    Guid ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal VatRate,
    decimal LineSubtotal, decimal LineVat, decimal LineTotal);

/// <summary>
/// A full invoice ("factura completa," AEAT TipoFactura "F1") — distinct from a
/// POS Sale (Domain.Sales), which produces simplified tickets ("F2"). An Invoice
/// always has a specific CustomerId (with NIF, needed for a real F1 invoice),
/// unlike a Sale which may be anonymous.
///
/// Core fields (lines, totals, customer) are immutable once constructed — same
/// discipline as Sale, for the same fiscal reason: a real invoice is never
/// edited, only cancelled (Cancel) or superseded by a rectificativa (MarkRectified
/// + a separate CreditNote aggregate, Phase 5 follow-up). Status and the
/// cancellation VeriFactu record id are the only things that change after
/// issuance.
/// </summary>
public class Invoice
{
    public Guid Id { get; }
    public Guid StoreId { get; }
    public Guid CustomerId { get; }
    public string InvoiceNumber { get; }
    public IReadOnlyList<InvoiceLine> Lines { get; }
    public decimal Subtotal { get; }
    public decimal VatTotal { get; }
    public decimal Total { get; }

    /// <summary>Null if this invoice wasn't created by converting a Quote (not
    /// currently possible via InvoiceService — direct invoice issuance without a
    /// prior quote is out of scope for this phase — but the field exists so the
    /// data model doesn't need to change if/when that's added).</summary>
    public Guid? SourceQuoteId { get; }

    public InvoiceStatus Status { get; private set; }
    public DateTimeOffset IssuedAt { get; }
    public Guid IdempotencyKey { get; }
    public Guid? VeriFactuRecordId { get; private set; }
    public Guid? CancellationVeriFactuRecordId { get; private set; }

    public Invoice(
        Guid id, Guid storeId, Guid customerId, string invoiceNumber,
        IReadOnlyList<InvoiceLine> lines, Guid? sourceQuoteId, Guid idempotencyKey,
        DateTimeOffset? issuedAt = null, InvoiceStatus? status = null,
        Guid? veriFactuRecordId = null, Guid? cancellationVeriFactuRecordId = null)
    {
        if (lines.Count == 0)
            throw new ArgumentException("An invoice must have at least one line.", nameof(lines));
        if (customerId == Guid.Empty)
            throw new ArgumentException("An invoice must be addressed to a specific customer.", nameof(customerId));

        Id = id;
        StoreId = storeId;
        CustomerId = customerId;
        InvoiceNumber = invoiceNumber;
        Lines = lines;
        SourceQuoteId = sourceQuoteId;
        IdempotencyKey = idempotencyKey;

        Subtotal = lines.Sum(l => l.LineSubtotal);
        VatTotal = lines.Sum(l => l.LineVat);
        Total = Subtotal + VatTotal;

        IssuedAt = issuedAt ?? DateTimeOffset.UtcNow;
        Status = status ?? InvoiceStatus.Issued;
        VeriFactuRecordId = veriFactuRecordId;
        CancellationVeriFactuRecordId = cancellationVeriFactuRecordId;
    }

    public void AttachVeriFactuRecord(Guid veriFactuRecordId)
    {
        if (VeriFactuRecordId is not null)
            throw new InvalidOperationException("A VeriFactu record is already attached to this invoice.");
        VeriFactuRecordId = veriFactuRecordId;
    }

    /// <summary>Cancelling an invoice is itself a fiscal event ("RF de anulación")
    /// — the caller (InvoiceService) must generate that record FIRST and pass its
    /// id here; this method doesn't generate anything, only records that it
    /// happened, same division of responsibility as AttachVeriFactuRecord.</summary>
    public void Cancel(Guid cancellationVeriFactuRecordId)
    {
        if (Status != InvoiceStatus.Issued)
            throw new InvalidOperationException($"Cannot cancel an invoice in status {Status} — only an Issued invoice can be cancelled.");
        Status = InvoiceStatus.Cancelled;
        CancellationVeriFactuRecordId = cancellationVeriFactuRecordId;
    }

    /// <summary>Called when a CreditNote (Phase 5 follow-up, not yet built) is
    /// issued against this invoice — marks it as superseded without cancelling
    /// it outright (cancellation and rectification are legally distinct events
    /// per the AEAT developer FAQ, see docs/PHASE5_NOTES.md).</summary>
    public void MarkRectified()
    {
        if (Status != InvoiceStatus.Issued)
            throw new InvalidOperationException($"Cannot mark an invoice in status {Status} as rectified — only an Issued invoice can be.");
        Status = InvoiceStatus.Rectified;
    }
}
