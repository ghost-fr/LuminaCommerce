namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Fields committed into the hash chain. This is a best-effort field set based on
/// the publicly known VeriFactu structure (issuer NIF, invoice ID, date, total,
/// previous record's hash) — NOT verified against the final published AEAT
/// technical specification (the "Reglamento" / XSD schemas). Flagged explicitly in
/// docs/PHASE2_3_NOTES.md: do not treat this as compliance-verified until checked
/// against the actual AEAT documentation before any production submission.
/// </summary>
public sealed record VeriFactuRecordInput(
    string IssuerNif,
    string InvoiceSeriesAndNumber,
    DateOnly IssueDate,
    decimal TotalAmount,
    string PreviousRecordHash); // "" (empty string) for the very first record in a chain

public sealed class VeriFactuRecord
{
    public Guid Id { get; }
    public Guid SifBoundaryId { get; } // the tenant/store/terminal this chain is scoped to
    public VeriFactuRecordInput Input { get; }
    public string RecordHash { get; }
    public DateTimeOffset GeneratedAt { get; }

    public VeriFactuRecord(Guid id, Guid sifBoundaryId, VeriFactuRecordInput input, string recordHash)
    {
        Id = id;
        SifBoundaryId = sifBoundaryId;
        Input = input;
        RecordHash = recordHash;
        GeneratedAt = DateTimeOffset.UtcNow;
    }
}
