using Lumina.Application.Ports;

namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Concrete implementation of Lumina.Application.Ports.IFiscalRecordGenerator.
/// GenerateCancellationAsync uses HashChainService.ComputeCancellationHash, which
/// was built and verified (AEAT Caso 3 fixed vector) during the hash-chain fix
/// but had no caller in the Application layer until now — InvoiceService.CancelAsync
/// is the first real caller.
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

        // Chain-integrity pre-check required by AEAT (OM HAC/1177/2024 art. 7.i)
        // is STILL NOT implemented here — same flagged gap as the hash-chain fix.
        // Not re-solved in this phase; see docs/PHASE5_NOTES.md.

        var input = new RegistrationRecordInput(
            request.IssuerNif, request.InvoiceSeriesAndNumber, request.IssueDate,
            request.InvoiceType, request.VatAmount, request.TotalAmount,
            previousHash, request.GeneratedAt);

        var recordId = Guid.NewGuid();
        var recordHash = _hashChain.ComputeRegistrationHash(input);
        var qrPayload = _qrBuilder.Build(input);

        await _chainStore.AppendAsync(sifBoundaryId, recordId, recordHash, ct);

        return new FiscalRecordResult(recordId, recordHash, qrPayload);
    }

    public async Task<FiscalCancellationResult> GenerateCancellationAsync(
        Guid sifBoundaryId, FiscalCancellationRequest request, CancellationToken ct = default)
    {
        // A cancellation record is itself a link in the SAME chain as
        // registration records — it reads the chain's current last hash and
        // becomes the new last hash, exactly like GenerateAsync above. It is
        // NOT a separate chain or a special case of the append logic.
        var previousHash = await _chainStore.GetLastHashAsync(sifBoundaryId, ct);

        var input = new CancellationRecordInput(
            request.IssuerNif, request.InvoiceSeriesAndNumber, request.IssueDate,
            previousHash, request.GeneratedAt);

        var recordId = Guid.NewGuid();
        var recordHash = _hashChain.ComputeCancellationHash(input);

        await _chainStore.AppendAsync(sifBoundaryId, recordId, recordHash, ct);

        return new FiscalCancellationResult(recordId, recordHash);
    }
}
