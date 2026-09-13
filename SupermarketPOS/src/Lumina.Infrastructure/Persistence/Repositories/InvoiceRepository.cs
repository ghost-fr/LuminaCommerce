using System.Text.Json;
using Lumina.Application.Ports;
using Lumina.Domain.Commercial;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly LuminaDbContext _db;
    private readonly NumberSequenceGenerator _sequences;
    public InvoiceRepository(LuminaDbContext db, NumberSequenceGenerator sequences)
    {
        _db = db;
        _sequences = sequences;
    }

    public async Task<Invoice?> FindByIdAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var record = await _db.InvoiceRecords.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        return record is null ? null : ToDomain(record);
    }

    public async Task<Invoice?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken ct = default)
    {
        var record = await _db.InvoiceRecords.FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, ct);
        return record is null ? null : ToDomain(record);
    }

    public async Task AddAsync(Invoice invoice, CancellationToken ct = default)
    {
        var record = new InvoiceRecord
        {
            Id = invoice.Id,
            StoreId = invoice.StoreId,
            CustomerId = invoice.CustomerId,
            InvoiceNumber = invoice.InvoiceNumber,
            LinesJson = JsonSerializer.Serialize(invoice.Lines.Select(l =>
                new InvoiceLineJson(l.ProductId, l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.LineSubtotal, l.LineVat, l.LineTotal))),
            SourceQuoteId = invoice.SourceQuoteId,
            Status = invoice.Status.ToString(),
            IssuedAt = invoice.IssuedAt,
            IdempotencyKey = invoice.IdempotencyKey,
            VeriFactuRecordId = invoice.VeriFactuRecordId,
            CancellationVeriFactuRecordId = invoice.CancellationVeriFactuRecordId
        };
        await _db.InvoiceRecords.AddAsync(record, ct);
    }

    public async Task UpdateStatusAsync(Invoice invoice, CancellationToken ct = default)
    {
        var record = await _db.InvoiceRecords.FirstOrDefaultAsync(i => i.Id == invoice.Id, ct)
            ?? throw new InvalidOperationException($"Cannot update Invoice {invoice.Id}: no persisted record found. Call AddAsync first.");

        // Only Status/CancellationVeriFactuRecordId — everything else on Invoice
        // is immutable by design (see Domain/Commercial/Invoice.cs), and this
        // repository deliberately offers no way to touch the rest.
        record.Status = invoice.Status.ToString();
        record.CancellationVeriFactuRecordId = invoice.CancellationVeriFactuRecordId;
    }

    public async Task<string> NextInvoiceNumberAsync(Guid storeId, CancellationToken ct = default)
    {
        // FIXED as part of the correctness-hardening pass — same fix, same
        // reasoning as SaleRepository.NextTicketNumberAsync.
        var next = await _sequences.NextAsync(storeId, "Invoice", ct);
        return $"FA-{next:D6}";
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    private static Invoice ToDomain(InvoiceRecord record)
    {
        var lines = (JsonSerializer.Deserialize<List<InvoiceLineJson>>(record.LinesJson) ?? new())
            .Select(l => new InvoiceLine(l.ProductId, l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.LineSubtotal, l.LineVat, l.LineTotal))
            .ToList();

        // Full constructor overload preserves original IssuedAt/Status/record ids
        // instead of stamping "now"/"Issued" — same reasoning as Sale's
        // rehydration constructor (Domain/Sales/Sale.cs).
        return new Invoice(
            record.Id, record.StoreId, record.CustomerId, record.InvoiceNumber,
            lines, record.SourceQuoteId, record.IdempotencyKey,
            issuedAt: record.IssuedAt,
            status: Enum.Parse<InvoiceStatus>(record.Status),
            veriFactuRecordId: record.VeriFactuRecordId,
            cancellationVeriFactuRecordId: record.CancellationVeriFactuRecordId);
    }
}
