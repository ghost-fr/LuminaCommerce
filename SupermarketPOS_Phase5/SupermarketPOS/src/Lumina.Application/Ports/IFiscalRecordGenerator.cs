namespace Lumina.Application.Ports;

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
/// Input for cancelling a previously-issued registration record ("RF de
/// anulación"). No amounts/invoice-type fields — confirmed against AEAT's own
/// worked example (Caso 3) that cancellation records carry a smaller, different
/// field set than registration, not a variant of it.
/// </summary>
public record FiscalCancellationRequest(
    string IssuerNif,
    string InvoiceSeriesAndNumber,
    DateOnly IssueDate,
    DateTimeOffset GeneratedAt);

public record FiscalCancellationResult(Guid VeriFactuRecordId, string RecordHash);

/// <summary>
/// Application-side port for fiscal record generation. Implemented by
/// Lumina.Fiscal. GenerateCancellationAsync is new as of Phase 5 — the
/// underlying hash computation (HashChainService.ComputeCancellationHash) was
/// built and verified during the hash-chain fix but had no caller until
/// InvoiceService.CancelAsync needed one.
/// </summary>
public interface IFiscalRecordGenerator
{
    Task<FiscalRecordResult> GenerateAsync(Guid sifBoundaryId, FiscalRecordRequest request, CancellationToken ct = default);
    Task<FiscalCancellationResult> GenerateCancellationAsync(Guid sifBoundaryId, FiscalCancellationRequest request, CancellationToken ct = default);
}

/// <summary>
/// KNOWN GAP, unchanged from the hash-chain fix: only exposes the last hash, not
/// enough to re-verify that the last record's OWN chain link is intact (AEAT
/// developer FAQ, art. 7.i). Not fixed in this phase either — still flagged.
/// </summary>
public interface IVeriFactuChainStore
{
    Task<string> GetLastHashAsync(Guid sifBoundaryId, CancellationToken ct = default);
    Task AppendAsync(Guid sifBoundaryId, Guid recordId, string recordHash, CancellationToken ct = default);
}
