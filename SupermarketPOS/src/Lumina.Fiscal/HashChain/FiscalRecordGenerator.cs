using Lumina.Application.Ports;

namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Concrete implementation of Lumina.Application.Ports.IFiscalRecordGenerator.
/// Generates registration ("RF de alta") records only — cancellation record
/// generation exists at the HashChainService level (verified, tested) but isn't
/// wired into this port yet, since nothing in the Application layer calls for a
/// cancellation flow (Phase 5+ scope, see docs/PHASE2_3_NOTES.md).
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
        var previousHash = await _chainStore.GetLastHashAsync(sifBoundaryId, ct);

        // Per AEAT OM HAC/1177/2024 art. 7.i (confirmed via the developer FAQ):
        // before generating any record other than the first in a chain, the
        // system must verify the previous record's own chain link is intact.
        // GetLastHashAsync currently returns only the last hash, not enough to
        // re-verify that record's OWN previous-hash link — a full implementation
        // of this check needs IVeriFactuChainStore to expose the last TWO records
        // (or the last record's stored previous-hash field) for comparison.
        // NOT implemented yet — flagged here explicitly rather than silently
        // skipped, since this is a specific, confirmed regulatory requirement,
        // not a nice-to-have. Tracked as an open item in docs/PHASE2_3_NOTES.md.

        var input = new RegistrationRecordInput(
            request.IssuerNif, request.InvoiceSeriesAndNumber, request.IssueDate,
            request.InvoiceType, request.VatAmount, request.TotalAmount,
            previousHash, request.GeneratedAt);

        var recordId = Guid.NewGuid();
        var recordHash = _hashChain.ComputeRegistrationHash(input);
        var qrPayload = _qrBuilder.Build(input);

        // Append happens here, inside record generation, so the chain link and
        // the hash it produced can never drift apart.
        await _chainStore.AppendAsync(sifBoundaryId, recordId, recordHash, ct);

        return new FiscalRecordResult(recordId, recordHash, qrPayload);
    }
}
