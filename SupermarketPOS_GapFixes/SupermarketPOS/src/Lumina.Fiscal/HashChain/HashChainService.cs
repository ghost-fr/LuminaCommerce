using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Computes the chained SHA-256 hash for a VeriFactu record. Format below is
/// VERIFIED (not inferred) against AEAT's own worked examples — see
/// HashChainServiceTests for the exact input strings and expected outputs, which
/// are AEAT's own documented values, independently reproduced here via SHA-256.
///
/// Canonical format: query-string style, "Campo=Valor" pairs joined with "&", no
/// trailing separator, UTF-8 encoded before hashing. Field order matters and is
/// fixed per record type (see RegistrationRecordInput / CancellationRecordInput).
///
/// Hash = SHA-256, UPPERCASE hex, 64 characters. (Convert.ToHexString returns
/// uppercase by default in .NET — the original version of this file called
/// .ToLowerInvariant() on the result, which was a real bug: AEAT's spec and every
/// worked example use uppercase. Fixed here, verified against real vectors.)
/// </summary>
public sealed class HashChainService
{
    public string ComputeRegistrationHash(RegistrationRecordInput input) =>
        ComputeSha256Uppercase(BuildRegistrationCanonicalString(input));

    public string ComputeCancellationHash(CancellationRecordInput input) =>
        ComputeSha256Uppercase(BuildCancellationCanonicalString(input));

    internal static string BuildRegistrationCanonicalString(RegistrationRecordInput input) =>
        BuildQueryString(
            ("IDEmisorFactura", input.IssuerNif),
            ("NumSerieFactura", input.InvoiceSeriesAndNumber),
            ("FechaExpedicionFactura", FormatDate(input.IssueDate)),
            ("TipoFactura", input.InvoiceType),
            ("CuotaTotal", FormatAmount(input.VatAmount)),
            ("ImporteTotal", FormatAmount(input.TotalAmount)),
            ("Huella", input.PreviousHash),
            ("FechaHoraHusoGenRegistro", FormatTimestamp(input.GeneratedAt)));

    internal static string BuildCancellationCanonicalString(CancellationRecordInput input) =>
        BuildQueryString(
            ("IDEmisorFacturaAnulada", input.IssuerNif),
            ("NumSerieFacturaAnulada", input.InvoiceSeriesAndNumber),
            ("FechaExpedicionFacturaAnulada", FormatDate(input.IssueDate)),
            ("Huella", input.PreviousHash),
            ("FechaHoraHusoGenRegistro", FormatTimestamp(input.GeneratedAt)));

    private static string BuildQueryString(params (string Name, string Value)[] fields) =>
        string.Join("&", fields.Select(f => $"{f.Name}={f.Value}"));

    private static string FormatDate(DateOnly date) =>
        date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    private static string FormatAmount(decimal amount) =>
        amount.ToString("F2", CultureInfo.InvariantCulture);

    // "zzz" gives an explicit +HH:mm offset (e.g. "+01:00") matching AEAT's
    // examples exactly — NOT "Z"/UTC-normalized. Whoever calls this must pass a
    // DateTimeOffset already converted to the relevant LOCAL timezone (the
    // store's, via Store.TimeZoneId), not raw UTC — see PosSaleService and
    // PHASE2_3_NOTES.md. Passing a UTC DateTimeOffset here would produce
    // "+00:00", which is syntactically valid but not verified to be what AEAT
    // actually wants for a Spain-based SIF.
    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string ComputeSha256Uppercase(string canonical)
    {
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes); // uppercase by default
    }
}
