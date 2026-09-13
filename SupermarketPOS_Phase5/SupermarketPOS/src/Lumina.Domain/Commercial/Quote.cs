namespace Lumina.Domain.Commercial;

public enum QuoteStatus
{
    Draft,
    Sent,
    Accepted,
    Rejected,
    Expired,
    ConvertedToInvoice
}

/// <summary>
/// A quote line. Unlike SaleLine (Domain.Sales), quotes carry a free-text
/// Description alongside ProductId — quotes are often negotiated/customized
/// before acceptance, so the line item text shown to the customer may differ
/// from the catalogue product name. Deliberately a separate type from SaleLine,
/// not shared, to keep the Sales and Commercial bounded contexts independent —
/// a change to one should never accidentally ripple into the other.
/// </summary>
public class QuoteLine
{
    public Guid ProductId { get; private set; }
    public string Description { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal VatRate { get; private set; }

    internal QuoteLine(Guid productId, string description, decimal quantity, decimal unitPrice, decimal vatRate)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Quote line description is required.", nameof(description));
        if (quantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (unitPrice < 0m)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");

        ProductId = productId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatRate = vatRate;
    }

    public decimal LineSubtotal => Math.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
    public decimal LineVat => Math.Round(LineSubtotal * VatRate, 2, MidpointRounding.AwayFromZero);
    public decimal LineTotal => LineSubtotal + LineVat;
}

/// <summary>
/// A quote/estimate ("presupuesto"). NOT a fiscal document — no VeriFactu record
/// is generated for a quote at any point in its lifecycle; fiscal obligations
/// only begin once (and if) it converts to an Invoice. This matters: a Quote can
/// be freely edited, re-sent, or abandoned with no fiscal trace, which is exactly
/// why it's a separate, simpler aggregate from Invoice rather than an "unissued
/// invoice" variant of the same type.
///
/// State machine: Draft -(Send)-> Sent -(Accept)-> Accepted -(ConvertToInvoice,
/// via InvoiceService)-> ConvertedToInvoice. Sent can also go to Rejected or
/// Expired. Only Draft is mutable (AddLine/RemoveLine).
/// </summary>
public class Quote
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid CustomerId { get; private set; }
    public QuoteStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ValidUntil { get; private set; }
    public Guid? ConvertedInvoiceId { get; private set; }

    private readonly List<QuoteLine> _lines = new();
    public IReadOnlyList<QuoteLine> Lines => _lines;

    private Quote() { }

    public Quote(Guid id, Guid storeId, Guid customerId, DateTimeOffset validUntil, DateTimeOffset? createdAt = null)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("A quote must be addressed to a specific customer — unlike a POS sale, quotes are never anonymous.", nameof(customerId));

        Id = id;
        StoreId = storeId;
        CustomerId = customerId;
        Status = QuoteStatus.Draft;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        ValidUntil = validUntil;
    }

    public QuoteLine AddLine(Guid productId, string description, decimal quantity, decimal unitPrice, decimal vatRate)
    {
        EnsureDraft();
        var line = new QuoteLine(productId, description, quantity, unitPrice, vatRate);
        _lines.Add(line);
        return line;
    }

    public void RemoveLine(Guid productId)
    {
        EnsureDraft();
        var line = _lines.FirstOrDefault(l => l.ProductId == productId)
            ?? throw new InvalidOperationException($"No quote line for product {productId}.");
        _lines.Remove(line);
    }

    public void Send()
    {
        if (Status != QuoteStatus.Draft)
            throw new InvalidOperationException($"Only a Draft quote can be sent (current status: {Status}).");
        if (_lines.Count == 0)
            throw new InvalidOperationException("Cannot send an empty quote.");
        Status = QuoteStatus.Sent;
    }

    public void Accept()
    {
        if (Status != QuoteStatus.Sent)
            throw new InvalidOperationException($"Only a Sent quote can be accepted (current status: {Status}).");
        Status = QuoteStatus.Accepted;
    }

    public void Reject()
    {
        if (Status != QuoteStatus.Sent)
            throw new InvalidOperationException($"Only a Sent quote can be rejected (current status: {Status}).");
        Status = QuoteStatus.Rejected;
    }

    /// <summary>Application layer calls this once ValidUntil has passed for a
    /// still-open (Sent) quote — this type has no clock of its own, so it never
    /// expires itself; something has to decide "now" is past ValidUntil and call this.</summary>
    public void MarkExpired()
    {
        if (Status != QuoteStatus.Sent)
            throw new InvalidOperationException($"Only a Sent quote can expire (current status: {Status}).");
        Status = QuoteStatus.Expired;
    }

    /// <summary>Called by InvoiceService after successfully creating the Invoice —
    /// this method only updates the Quote's own state, it does not create anything.</summary>
    public void MarkConverted(Guid invoiceId)
    {
        if (Status != QuoteStatus.Accepted)
            throw new InvalidOperationException($"Only an Accepted quote can convert to an invoice (current status: {Status}).");
        Status = QuoteStatus.ConvertedToInvoice;
        ConvertedInvoiceId = invoiceId;
    }

    private void EnsureDraft()
    {
        if (Status != QuoteStatus.Draft)
            throw new InvalidOperationException($"Cannot modify a quote in status {Status} — only Draft quotes can be edited.");
    }

    public decimal Subtotal => _lines.Sum(l => l.LineSubtotal);
    public decimal VatTotal => _lines.Sum(l => l.LineVat);
    public decimal Total => Subtotal + VatTotal;
}
