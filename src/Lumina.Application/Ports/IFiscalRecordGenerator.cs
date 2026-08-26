namespace Lumina.Application.Ports;

public record FiscalRecordRequest(
    string IssuerNif, string InvoiceSeriesAndNumber, DateOnly IssueDate, decimal TotalAmount);

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
/// Lumina.Fiscal's IFiscalRecordGenerator implementation (Fiscal already
/// references Application, so it can depend on this port without Fiscal ever
/// referencing Infrastructure directly).
/// </summary>
public interface IVeriFactuChainStore
{
    /// <summary>Empty string if this is the first record ever for this boundary.</summary>
    Task<string> GetLastHashAsync(Guid sifBoundaryId, CancellationToken ct = default);

    Task AppendAsync(Guid sifBoundaryId, Guid recordId, string recordHash, CancellationToken ct = default);
}
