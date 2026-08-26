using System.Security.Cryptography;
using System.Text;

namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Computes the chained SHA-256 hash for a VeriFactu record. The canonical string
/// format below is an INTERNAL convention for this codebase, not dictated by AEAT —
/// AEAT specifies WHICH fields must be chained and hashed, but implementers choose
/// their own canonical serialization as long as it's deterministic and documented.
/// This format must be treated as frozen once any real invoicing record is issued
/// against it in a non-dev environment: changing the format changes every future
/// hash even though the underlying data hasn't changed, which would look like chain
/// tampering to anyone verifying it. If it must change, that's a new SIF boundary /
/// new chain, not a silent format tweak.
///
/// Canonical format (pipe-delimited, UTF-8, exact field order):
///   {IssuerNif}|{InvoiceSeriesAndNumber}|{IssueDate:yyyy-MM-dd}|{TotalAmount:F2}|{PreviousRecordHash}
///
/// Hash = lowercase hex SHA-256 of the UTF-8 bytes of that string.
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
        var amountPart = input.TotalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        return $"{input.IssuerNif}|{input.InvoiceSeriesAndNumber}|{datePart}|{amountPart}|{input.PreviousRecordHash}";
    }

    public VeriFactuRecord CreateRecord(Guid id, Guid sifBoundaryId, VeriFactuRecordInput input)
    {
        var hash = ComputeHash(input);
        return new VeriFactuRecord(id, sifBoundaryId, input, hash);
    }
}
