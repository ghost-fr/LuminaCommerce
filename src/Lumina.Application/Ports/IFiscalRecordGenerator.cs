namespace Lumina.Application.Ports;

public record FiscalRecordRequest(
    string IssuerNif, string InvoiceSeriesAndNumber, DateOnly IssueDate, string InvoiceType,
    decimal TotalTaxAmount, decimal TotalAmount);

public record FiscalRecordResult(
    Guid VeriFactuRecordId,
    string RecordHash,
    string QrPayload,
    bool ChainIntegrityOk,
    string? ChainIntegrityIssue);

public interface IFiscalRecordGenerator
{
    Task<FiscalRecordResult> GenerateAsync(Guid sifBoundaryId, FiscalRecordRequest request, CancellationToken ct = default);
}

public record VeriFactuChainLink(string RecordHash, string PreviousRecordHash, DateTimeOffset RecordGeneratedAt);

public interface IVeriFactuChainStore
{
    Task<VeriFactuChainLink?> GetLastLinkAsync(Guid sifBoundaryId, CancellationToken ct = default);
    Task<VeriFactuChainLink?> GetSecondToLastLinkAsync(Guid sifBoundaryId, CancellationToken ct = default);
    Task AppendAsync(Guid sifBoundaryId, Guid recordId, string recordHash, string previousRecordHash, DateTimeOffset recordGeneratedAt, CancellationToken ct = default);
}
