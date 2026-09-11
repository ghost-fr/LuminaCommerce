using System.Security.Cryptography;
using System.Text;

namespace Lumina.Fiscal.HashChain;

/// <summary>
/// 8-field canonical SHA-256 chain per Orden HAC/1177/2024 art. 13.1.a).
/// From Claude AEAT readiness patch. Confirm formatting against AEAT worked examples.
/// </summary>
public sealed class HashChainService
{
    public string ComputeHash(VeriFactuRecordInput input)
    {
        var canonical = BuildCanonicalString(input);
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    internal static string BuildCanonicalString(VeriFactuRecordInput input)
    {
        var datePart = input.IssueDate.ToString("yyyy-MM-dd");
        var taxAmountPart = input.TotalTaxAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var amountPart = input.TotalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var generatedAtPart = input.RecordGeneratedAt.ToString("O");
        return $"{input.IssuerNif}|{input.InvoiceSeriesAndNumber}|{datePart}|{input.InvoiceType}|" +
               $"{taxAmountPart}|{amountPart}|{input.PreviousRecordHash}|{generatedAtPart}";
    }

    public VeriFactuRecord CreateRecord(Guid id, Guid sifBoundaryId, VeriFactuRecordInput input)
    {
        var hash = ComputeHash(input);
        return new VeriFactuRecord(id, sifBoundaryId, input, hash);
    }
}
