using Lumina.Application.Ports;

namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Chain integrity pre-check + 8-field hash generation.
/// FiscalRecordGenerator.cs was corrupted in Drive zip; reconstructed to match ports.
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
        var integrityOk = true;
        string? integrityIssue = null;

        var last = await _chainStore.GetLastLinkAsync(sifBoundaryId, ct);
        if (last is not null)
        {
            if (last.RecordGeneratedAt > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                integrityOk = false;
                integrityIssue = $"Last chain record timestamp is ahead of now ({last.RecordGeneratedAt:O}).";
            }

            var second = await _chainStore.GetSecondToLastLinkAsync(sifBoundaryId, ct);
            if (second is not null &&
                !string.Equals(last.PreviousRecordHash, second.RecordHash, StringComparison.Ordinal))
            {
                integrityOk = false;
                integrityIssue = (integrityIssue is null ? "" : integrityIssue + " ") +
                    "Last link PreviousRecordHash does not match prior RecordHash.";
            }
        }

        var previousHash = last?.RecordHash ?? string.Empty;
        var generatedAt = DateTimeOffset.Now;

        var input = new VeriFactuRecordInput(
            request.IssuerNif,
            request.InvoiceSeriesAndNumber,
            request.IssueDate,
            request.InvoiceType,
            request.TotalTaxAmount,
            request.TotalAmount,
            previousHash,
            generatedAt);

        var recordId = Guid.NewGuid();
        var recordHash = _hashChain.ComputeHash(input);
        var qrPayload = _qrBuilder.Build(input);

        await _chainStore.AppendAsync(
            sifBoundaryId, recordId, recordHash, previousHash, generatedAt, ct);

        if (!integrityOk)
            Console.Error.WriteLine($"[VeriFactu chain integrity] {integrityIssue}");

        return new FiscalRecordResult(recordId, recordHash, qrPayload, integrityOk, integrityIssue);
    }
}
