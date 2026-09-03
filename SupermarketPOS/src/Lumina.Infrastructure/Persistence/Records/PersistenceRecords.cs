namespace Lumina.Infrastructure.Persistence.Records;

/// <summary>
/// Plain EF-mapped persistence shapes for Cart and Sale. Deliberately NOT the domain
/// aggregates themselves — Cart/Sale use private setters and constructor invariants
/// (per the blueprint's "rich domain, no DataRows" principle), which EF Core can map
/// via backing fields but only after verifying the mapping compiles and round-trips
/// with a live `dotnet ef` tool (not available in the environment that wrote this).
/// Instead: persist a flat/JSON projection here, and have the repository translate
/// to/from the real aggregate using its public API (Cart's constructor + AddLine +
/// SetCustomer are all public, so this requires no domain changes). Sale is
/// reconstructed via its public constructor + AttachVeriFactuRecord.
/// This trades a small amount of JSON (de)serialization for avoiding a fragile,
/// unverified EF mapping of aggregate internals — worth revisiting once `dotnet ef`
/// is available locally and the mapping can actually be tested.
/// </summary>
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

/// <summary>
/// UPDATED as part of the correctness-hardening pass: gained PreviousHash and
/// GeneratedAt. Without PreviousHash stored per link, there was no way to
/// verify a past record's own chain consistency — the store could only ever
/// answer "what's the last hash," never "is the chain actually intact."
/// GeneratedAt is the business timestamp passed into hash generation
/// (distinct from CreatedAt, the DB row's insertion time — currently always
/// the same instant in practice since generation is synchronous, but
/// conceptually different and worth keeping separate).
/// </summary>
public class VeriFactuChainRecord
{
    public Guid SifBoundaryId { get; set; }
    public Guid RecordId { get; set; }
    public string RecordHash { get; set; } = string.Empty;
    public string PreviousHash { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
