using System.Globalization;

namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Builds the QR verification URL. VERIFIED against AEAT's own Java example
/// ("Ejemplo de tratamiento de URL encoding en lenguaje java"): base URL,
/// parameter names (nif, numserie, fecha, importe), parameter order, dd-MM-yyyy
/// date format, and two-decimal amount format all confirmed to match exactly —
/// including a chosen example value containing a literal "&amp;" specifically to
/// prove per-parameter encoding is correct (see the escaping test).
///
/// Only open item: the exact production vs. sandbox base URL — AEAT's example
/// uses "prewww2.aeat.es" (their preproduction/test host); a production system
/// needs the real production host confirmed before going live, likely
/// "www?.agenciatributaria.gob.es" or similar — not yet confirmed against a
/// production-specific document.
/// </summary>
public sealed class QrPayloadBuilder
{
    // Confirmed against AEAT's own Java example — this is their preproduction/
    // sandbox host. Swap for the confirmed production host before any real
    // (non-test) invoice uses this — see class remarks above.
    private const string VerificationBaseUrl = "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR";

    public string Build(RegistrationRecordInput record)
    {
        var nif = Uri.EscapeDataString(record.IssuerNif);
        var numSerie = Uri.EscapeDataString(record.InvoiceSeriesAndNumber);
        var fecha = Uri.EscapeDataString(record.IssueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
        var importe = Uri.EscapeDataString(record.TotalAmount.ToString("F2", CultureInfo.InvariantCulture));

        return $"{VerificationBaseUrl}?nif={nif}&numserie={numSerie}&fecha={fecha}&importe={importe}";
    }
}
