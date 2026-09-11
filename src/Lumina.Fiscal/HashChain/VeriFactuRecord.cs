namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Fields committed into the hash chain, per Orden HAC/1177/2024, art. 13.1.a).
/// </summary>
public sealed record VeriFactuRecordInput(
    string IssuerNif,
    string InvoiceSeriesAndNumber,
    DateOnly IssueDate,
    string InvoiceType,
    decimal TotalTaxAmount,
    decimal TotalAmount,
    string PreviousRecordHash,
    DateTimeOffset RecordGeneratedAt);

public sealed class VeriFactuRecord
{
    public Guid Id { get; }
    public Guid SifBoundaryId { get; }
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
