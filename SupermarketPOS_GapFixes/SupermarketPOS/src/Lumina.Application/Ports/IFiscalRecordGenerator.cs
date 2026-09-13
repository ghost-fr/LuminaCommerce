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

public record FiscalCancellationRequest(
    string IssuerNif,
    string InvoiceSeriesAndNumber,
    DateOnly IssueDate,
    DateTimeOffset GeneratedAt);

public record FiscalCancellationResult(Guid VeriFactuRecordId, string RecordHash);

public interface IFiscalRecordGenerator
{
    Task<FiscalRecordResult> GenerateAsync(Guid sifBoundaryId, FiscalRecordRequest request, CancellationToken ct = default);
    Task<FiscalCancellationResult> GenerateCancellationAsync(Guid sifBoundaryId, FiscalCancellationRequest request, CancellationToken ct = default);
}

/// <summary>One link in a SIF boundary's hash chain, as needed for the
/// chain-integrity pre-check (see FiscalChainIntegrityException below).</summary>
public record ChainLink(string RecordHash, string PreviousHash, DateTimeOffset GeneratedAt);

/// <summary>
/// UPDATED as part of the correctness-hardening pass: AppendAsync now stores
/// PreviousHash and GeneratedAt (previously only RecordId/RecordHash were
/// stored) — without these, a chain-integrity check has nothing to verify
/// against. GetRecentLinksAsync is new, replacing the old GetLastHashAsync-only
/// shape (kept as a convenience overload below, implemented in terms of the new
/// method, so nothing calling the old signature breaks).
/// </summary>
public interface IVeriFactuChainStore
{
    /// <summary>Empty string if this is the first record ever for this boundary.</summary>
    Task<string> GetLastHashAsync(Guid sifBoundaryId, CancellationToken ct = default);

    /// <summary>Up to `count` most recent links for this boundary, ordered
    /// MOST-RECENT-FIRST (index 0 = last record, index 1 = second-to-last if
    /// it exists). Backs the AEAT-required chain-integrity pre-check (OM
    /// HAC/1177/2024 art. 7.i) — see FiscalRecordGenerator.</summary>
    Task<IReadOnlyList<ChainLink>> GetRecentLinksAsync(Guid sifBoundaryId, int count, CancellationToken ct = default);

    Task AppendAsync(
        Guid sifBoundaryId, Guid recordId, string recordHash, string previousHash, DateTimeOffset generatedAt,
        CancellationToken ct = default);
}

/// <summary>
/// Thrown when the AEAT-required chain-integrity pre-check fails — either the
/// chain's own internal consistency is broken (a record's claimed
/// PreviousHash doesn't match what the actual previous record's hash is), or
/// the timestamp-monotonicity check fails. This is a distinct, catchable type
/// specifically so callers (PosSaleService, QuoteService, InvoiceService) can
/// map it to RejectionCode.FiscalChainError — an enum value that existed
/// since Phase 3 but had no code path that ever triggered it until this fix.
/// </summary>
public sealed class FiscalChainIntegrityException : Exception
{
    public FiscalChainIntegrityException(string message) : base(message) { }
}
