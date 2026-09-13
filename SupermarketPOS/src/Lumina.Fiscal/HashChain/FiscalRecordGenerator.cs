using Lumina.Application.Ports;

namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Concrete implementation of Lumina.Application.Ports.IFiscalRecordGenerator.
///
/// UPDATED as part of the correctness-hardening pass: both GenerateAsync and
/// GenerateCancellationAsync now run VerifyChainIntegrityAsync before
/// generating anything — this was previously an explicitly flagged, unbuilt
/// gap (see HASH_CHAIN_FIX_NOTES.md / PHASE5_NOTES.md). It's now real.
/// </summary>
public sealed class FiscalRecordGenerator : IFiscalRecordGenerator
{
    private readonly HashChainService _hashChain;
    private readonly QrPayloadBuilder _qrBuilder;
    private readonly IVeriFactuChainStore _chainStore;

    public FiscalRecordGenerator(HashChainService hashChain, QrPayloadBuilder qrBuilder, IVeriFactuChainStore chainStore)
    {
        _hashChain = hashChain;
        _qrBuilder = qrBuilder;
        _chainStore = chainStore;
    }

    public async Task<FiscalRecordResult> GenerateAsync(
        Guid sifBoundaryId, FiscalRecordRequest request, CancellationToken ct = default)
    {
        await VerifyChainIntegrityAsync(sifBoundaryId, request.GeneratedAt, ct);

        var previousHash = await _chainStore.GetLastHashAsync(sifBoundaryId, ct);

        var input = new RegistrationRecordInput(
            request.IssuerNif, request.InvoiceSeriesAndNumber, request.IssueDate,
            request.InvoiceType, request.VatAmount, request.TotalAmount,
            previousHash, request.GeneratedAt);

        var recordId = Guid.NewGuid();
        var recordHash = _hashChain.ComputeRegistrationHash(input);
        var qrPayload = _qrBuilder.Build(input);

        await _chainStore.AppendAsync(sifBoundaryId, recordId, recordHash, previousHash, request.GeneratedAt, ct);

        return new FiscalRecordResult(recordId, recordHash, qrPayload);
    }

    public async Task<FiscalCancellationResult> GenerateCancellationAsync(
        Guid sifBoundaryId, FiscalCancellationRequest request, CancellationToken ct = default)
    {
        await VerifyChainIntegrityAsync(sifBoundaryId, request.GeneratedAt, ct);

        var previousHash = await _chainStore.GetLastHashAsync(sifBoundaryId, ct);

        var input = new CancellationRecordInput(
            request.IssuerNif, request.InvoiceSeriesAndNumber, request.IssueDate,
            previousHash, request.GeneratedAt);

        var recordId = Guid.NewGuid();
        var recordHash = _hashChain.ComputeCancellationHash(input);

        await _chainStore.AppendAsync(sifBoundaryId, recordId, recordHash, previousHash, request.GeneratedAt, ct);

        return new FiscalCancellationResult(recordId, recordHash);
    }

    /// <summary>
    /// Implements the two checks required by AEAT OM HAC/1177/2024 art. 7.i
    /// (confirmed via AEAT's own developer FAQ, "Aclaraciones a dudas de los
    /// desarrolladores") before generating any record beyond the first in a
    /// chain:
    ///
    /// 1. CHAIN CONSISTENCY (high confidence — this is a straightforward
    ///    structural check, not a judgment call): if a second-to-last record
    ///    exists, its actual hash must equal what the last record claims as
    ///    ITS PreviousHash. A mismatch means the chain has been corrupted or
    ///    tampered with (e.g. a direct DB edit bypassing the application) —
    ///    this is exactly the failure mode the whole hash-chain mechanism
    ///    exists to detect.
    ///
    /// 2. TIMESTAMP MONOTONICITY (best-effort interpretation — flagged with
    ///    LOWER confidence than the chain-consistency check or the hash
    ///    algorithm itself, which were verified against exact AEAT worked
    ///    examples with independently-reproduced hash values; this check is
    ///    built from a paraphrased reading of the developer FAQ, not a
    ///    verbatim-verified worked example): the previous record's
    ///    timestamp must not be more than one minute AHEAD of the new
    ///    record's timestamp. If this specific tolerance or direction turns
    ///    out to be wrong once the exact FAQ wording is re-checked against
    ///    the primary PDF, this is the method to correct — the chain-
    ///    consistency check above is independent of it and stays correct
    ///    either way.
    /// </summary>
    private async Task VerifyChainIntegrityAsync(Guid sifBoundaryId, DateTimeOffset newRecordGeneratedAt, CancellationToken ct)
    {
        var recent = await _chainStore.GetRecentLinksAsync(sifBoundaryId, 2, ct);
        if (recent.Count == 0) return; // first record ever for this boundary — nothing to verify against

        var last = recent[0];

        if (recent.Count >= 2)
        {
            var secondToLast = recent[1];
            if (secondToLast.RecordHash != last.PreviousHash)
            {
                throw new FiscalChainIntegrityException(
                    $"VeriFactu chain integrity check failed for SIF boundary {sifBoundaryId}: the last " +
                    $"record's stored PreviousHash ({last.PreviousHash}) does not match the second-to-last " +
                    $"record's actual hash ({secondToLast.RecordHash}). This indicates the chain has been " +
                    "tampered with or corrupted. Per AEAT OM HAC/1177/2024 art. 7.i, no new record may be " +
                    "generated until this is resolved — this is not a normal business rejection, it requires " +
                    "investigation.");
            }
        }

        if (last.GeneratedAt > newRecordGeneratedAt.AddMinutes(1))
        {
            throw new FiscalChainIntegrityException(
                $"VeriFactu chain integrity check failed for SIF boundary {sifBoundaryId}: the previous " +
                $"record's timestamp ({last.GeneratedAt:O}) is more than one minute ahead of the new " +
                $"record's timestamp ({newRecordGeneratedAt:O}). Per AEAT OM HAC/1177/2024 art. 7.i this " +
                "indicates a clock or ordering problem that must be resolved before a new record is generated.");
        }
    }
}
