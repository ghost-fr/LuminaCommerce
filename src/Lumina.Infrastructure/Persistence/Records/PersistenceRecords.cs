namespace Lumina.Infrastructure.Persistence.Records;

public class CartRecord
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid? CustomerId { get; set; }
    public string LinesJson { get; set; } = "[]";
}

public record CartLineJson(Guid ProductId, decimal Quantity, decimal UnitPrice, decimal VatRate, string? PromotionCode);

public class SaleRecord
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid RegisterId { get; set; }
    public Guid? CustomerId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string LinesJson { get; set; } = "[]";
    public string TendersJson { get; set; } = "[]";
    public Guid IdempotencyKey { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public Guid? VeriFactuRecordId { get; set; }
}

public record SaleLineJson(
    Guid ProductId, decimal Quantity, decimal UnitPrice, decimal VatRate,
    decimal LineSubtotal, decimal LineVat, decimal LineTotal, string? PromotionCode);

public record SaleTenderJson(string TenderType, decimal Amount, string? Reference);

/// <summary>Per-store gap-free ticket sequence (SaleRepository.NextTicketNumberAsync).</summary>
public class TicketSequenceRecord
{
    public Guid StoreId { get; set; }
    public long NextNumber { get; set; }
}

public class VeriFactuChainRecord
{
    public Guid SifBoundaryId { get; set; }
    public Guid RecordId { get; set; }
    public string RecordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
