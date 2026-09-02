namespace Lumina.Infrastructure.Persistence.Records;

/// <summary>
/// Persistence shapes for Quote and Invoice — same JSON-projection pattern as
/// Cart/Sale (Records/PersistenceRecords.cs) and for the same reason: both
/// aggregates use private setters and encapsulated collections, and this trades
/// a small amount of JSON (de)serialization for avoiding a fragile, unverified
/// EF mapping of aggregate internals.
/// </summary>
public class QuoteRecord
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public Guid? ConvertedInvoiceId { get; set; }
    public string LinesJson { get; set; } = "[]";
}

public record QuoteLineJson(Guid ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal VatRate);

public class InvoiceRecord
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string LinesJson { get; set; } = "[]";
    public Guid? SourceQuoteId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; }
    public Guid IdempotencyKey { get; set; }
    public Guid? VeriFactuRecordId { get; set; }
    public Guid? CancellationVeriFactuRecordId { get; set; }
}

public record InvoiceLineJson(
    Guid ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal VatRate,
    decimal LineSubtotal, decimal LineVat, decimal LineTotal);
