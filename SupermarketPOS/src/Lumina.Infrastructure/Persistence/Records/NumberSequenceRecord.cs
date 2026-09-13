namespace Lumina.Infrastructure.Persistence.Records;

/// <summary>One row per (StoreId, SequenceType) — e.g. ("Ticket", "Invoice").
/// Backs NumberSequenceGenerator's atomic increment. See that class for why
/// this replaces the previous count-existing-rows approach.</summary>
public class NumberSequenceRecord
{
    public Guid StoreId { get; set; }
    public string SequenceType { get; set; } = string.Empty;
    public long CurrentValue { get; set; }
}
