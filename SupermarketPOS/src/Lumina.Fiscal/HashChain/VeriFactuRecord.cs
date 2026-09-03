namespace Lumina.Fiscal.HashChain;

/// <summary>
/// Input for a registration record ("RF de alta"). Field set, order, and formats
/// below are VERIFIED against AEAT's own worked examples in "Detalle de las
/// especificaciones técnicas... Algoritmo de cálculo de codificación de la huella"
/// (Caso 2) — not inferred, not guessed. See HashChainServiceTests for the exact
/// fixed vector this was checked against (input string → SHA-256 → matches AEAT's
/// documented output exactly).
/// </summary>
public sealed record RegistrationRecordInput(
    string IssuerNif,                  // IDEmisorFactura
    string InvoiceSeriesAndNumber,     // NumSerieFactura
    DateOnly IssueDate,                // FechaExpedicionFactura, formatted dd-MM-yyyy
    string InvoiceType,                // TipoFactura — short code, e.g. "F1", "F2" (see PHASE2_3_NOTES.md re: which to use)
    decimal VatAmount,                 // CuotaTotal
    decimal TotalAmount,               // ImporteTotal
    string PreviousHash,               // Huella — "" for the very first record in a chain
    DateTimeOffset GeneratedAt);       // FechaHoraHusoGenRegistro, ISO 8601 with explicit UTC offset (NOT normalized to UTC — see notes)

/// <summary>
/// Input for a cancellation record ("RF de anulación") — a genuinely different
/// field set from registration, not a variant of it (confirmed: fewer fields, no
/// TipoFactura/CuotaTotal/ImporteTotal, field names carry an "Anulada" suffix).
/// VERIFIED against AEAT's Caso 3 worked example the same way as registration.
/// Nothing in the Application layer generates one of these yet — the
/// rectification/cancellation workflow (RF de anulación, RF de alta de
/// subsanación) is Phase 5+ scope. This type exists now because the hash-chain
/// mechanism itself needed to be built correctly for both record shapes, not
/// because a cancellation flow is wired up anywhere.
/// </summary>
public sealed record CancellationRecordInput(
    string IssuerNif,                  // IDEmisorFacturaAnulada
    string InvoiceSeriesAndNumber,     // NumSerieFacturaAnulada
    DateOnly IssueDate,                // FechaExpedicionFacturaAnulada, dd-MM-yyyy
    string PreviousHash,               // Huella
    DateTimeOffset GeneratedAt);       // FechaHoraHusoGenRegistro

public sealed class VeriFactuRecord
{
    public Guid Id { get; }
    public Guid SifBoundaryId { get; }
    public string RecordHash { get; }
    public DateTimeOffset GeneratedAt { get; }

    public VeriFactuRecord(Guid id, Guid sifBoundaryId, string recordHash, DateTimeOffset generatedAt)
    {
        Id = id;
        SifBoundaryId = sifBoundaryId;
        RecordHash = recordHash;
        GeneratedAt = generatedAt;
    }
}
