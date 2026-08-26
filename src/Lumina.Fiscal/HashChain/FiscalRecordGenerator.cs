using Lumina.Application.Ports;

namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Concrete implementation of Lumina.Application.Ports.IFiscalRecordGenerator.
/// Fetches the last hash for the SIF boundary, computes the new chained hash,
/// builds the QR payload, persists the new chain link, and returns everything
/// PosSaleService needs to attach to the Sale aggregate.
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

        var input = new VeriFactuRecordInput(
            request.IssuerNif, request.InvoiceSeriesAndNumber, request.IssueDate,
            request.TotalAmount, previousHash);

        var recordId = Guid.NewGuid();
        var recordHash = _hashChain.ComputeHash(input);
        var qrPayload = _qrBuilder.Build(input);

        // Append happens here, inside record generation, so the chain link and the
        // hash it produced can never drift apart — no caller can compute a hash and
        // forget to persist it (or persist without computing), which would silently
        // corrupt the chain for every subsequent record.
        await _chainStore.AppendAsync(sifBoundaryId, recordId, recordHash, ct);

        return new FiscalRecordResult(recordId, recordHash, qrPayload);
    }
}
