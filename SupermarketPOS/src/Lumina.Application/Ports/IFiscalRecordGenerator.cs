namespace Lumina.Application.Ports;

/// <summary>
/// Fields match AEAT's verified registration-record canonical format exactly
/// (see Lumina.Fiscal.HashChain.RegistrationRecordInput) — InvoiceType and
/// VatAmount are new as of the hash-chain fix; previously this only carried a
/// single TotalAmount and no invoice-type code, which was an incomplete/incorrect
/// shape now corrected against real AEAT worked examples.
/// </summary>
public record FiscalRecordRequest(
    string IssuerNif,
    string InvoiceSeriesAndNumber,
    DateOnly IssueDate,
    string InvoiceType,
    decimal VatAmount,
    decimal TotalAmount,
    DateTimeOffset GeneratedAt);

public record FiscalRecordResult(Guid VeriFactuRecordId, string RecordHash, string QrPayload);

/// <summary>
/// Application-side port for fiscal record generation. Implemented by
/// Lumina.Fiscal (which references Application to implement this — same
/// direction as Infrastructure implementing IUserRepository). PosSaleService
/// depends only on this interface, never on Lumina.Fiscal types directly, which
/// keeps the Application layer's dependency graph acyclic: Fiscal -> Application,
/// never Application -> Fiscal.
/// </summary>
public interface IFiscalRecordGenerator
{
    Task<FiscalRecordResult> GenerateAsync(Guid sifBoundaryId, FiscalRecordRequest request, CancellationToken ct = default);
}

/// <summary>
/// Persistence port for the hash chain's "last hash per SIF boundary" state and
/// the VeriFactu records themselves. Implemented by Infrastructure; consumed by
/// Lumina.Fiscal's IFiscalRecordGenerator implementation.
///
/// KNOWN GAP: only exposes the last hash, not enough to re-verify that the last
/// record's OWN chain link is intact (a confirmed AEAT requirement, art. 7.i —
/// see FiscalRecordGenerator.cs). A full fix likely needs this to expose the
/// last record's (Hash, PreviousHash) pair, not just Hash. Not fixed in this
/// patch — flagged as an open item.
/// </summary>
public interface IVeriFactuChainStore
{
    /// <summary>Empty string if this is the first record ever for this boundary.</summary>
    Task<string> GetLastHashAsync(Guid sifBoundaryId, CancellationToken ct = default);

    Task AppendAsync(Guid sifBoundaryId, Guid recordId, string recordHash, CancellationToken ct = default);
}
