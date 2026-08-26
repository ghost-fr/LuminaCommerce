using System.Globalization;

namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Builds the QR payload embedded on tickets/invoices per the general shape of
/// VeriFactu's published QR requirement: a URL to AEAT's verification endpoint
/// carrying NIF, invoice series+number, date, and total as query parameters.
///
/// *** COMPLIANCE WARNING — READ BEFORE ANY PRODUCTION USE ***
/// The exact query parameter names, the verification endpoint URL, required legend
/// text ("Factura verificable en la sede electrónica de la AEATVERI*FACTU"), and
/// QR rendering rules (minimum size, quiet zone, error-correction level) are set by
/// AEAT's published technical documentation (Orden HAC/1177/2024 and its XSD/PDF
/// annexes). This implementation was written from general knowledge of the
/// VeriFactu program, NOT from the current authoritative spec document, because
/// verifying the exact current spec requires checking AEAT's published materials
/// directly. Before this is used for a single real invoice: pull the current
/// spec from https://sede.agenciatributaria.gob.es (VERI*FACTU section) and
/// diff every field/URL/format below against it. Treat this as a structural
/// placeholder, not a finished compliance artifact.
/// </summary>
public sealed class QrPayloadBuilder
{
    // PLACEHOLDER — replace with the real AEAT verification base URL once confirmed
    // against current published spec.
    private const string VerificationBaseUrl = "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR";

    public string Build(VeriFactuRecordInput record)
    {
        // Manual query-string construction with Uri.EscapeDataString — avoids a
        // dependency on System.Web.HttpUtility, which isn't available in a plain
        // net8.0 desktop project without pulling in ASP.NET Core.
        var nif = Uri.EscapeDataString(record.IssuerNif);
        var numSerie = Uri.EscapeDataString(record.InvoiceSeriesAndNumber);
        var fecha = Uri.EscapeDataString(record.IssueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
        var importe = Uri.EscapeDataString(record.TotalAmount.ToString("F2", CultureInfo.InvariantCulture));

        return $"{VerificationBaseUrl}?nif={nif}&numserie={numSerie}&fecha={fecha}&importe={importe}";
    }
}
