using System.Text.Json;
using Lumina.Application.Ports;
using Lumina.Domain.Sales;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

public class SaleRepository : ISaleRepository
{
    private readonly LuminaDbContext _db;
    private readonly NumberSequenceGenerator _sequences;
    public SaleRepository(LuminaDbContext db, NumberSequenceGenerator sequences)
    {
        _db = db;
        _sequences = sequences;
    }

    public async Task AddAsync(Sale sale, CancellationToken ct = default)
    {
        var record = new SaleRecord
        {
            Id = sale.Id,
            StoreId = sale.StoreId,
            RegisterId = sale.RegisterId,
            CustomerId = sale.CustomerId,
            TicketNumber = sale.TicketNumber,
            LinesJson = JsonSerializer.Serialize(sale.Lines.Select(l =>
                new SaleLineJson(l.ProductId, l.Quantity, l.UnitPrice, l.VatRate, l.LineSubtotal, l.LineVat, l.LineTotal, l.PromotionCode))),
            TendersJson = JsonSerializer.Serialize(sale.Tenders.Select(t =>
                new SaleTenderJson(t.TenderType, t.Amount, t.Reference))),
            IdempotencyKey = sale.IdempotencyKey,
            CompletedAt = sale.CompletedAt,
            VeriFactuRecordId = sale.VeriFactuRecordId
        };

        await _db.SaleRecords.AddAsync(record, ct);
    }

    public async Task<Sale?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken ct = default)
    {
        var record = await _db.SaleRecords.FirstOrDefaultAsync(s => s.IdempotencyKey == idempotencyKey, ct);
        return record is null ? null : ToDomain(record);
    }

    public async Task<string> NextTicketNumberAsync(Guid storeId, CancellationToken ct = default)
    {
        // FIXED as part of the correctness-hardening pass — was previously
        // "count existing rows," a real race condition under concurrent
        // registers (flagged since Phase 2/3). Now backed by
        // NumberSequenceGenerator's atomic DB-transaction increment. See that
        // class for exactly why it's safe under concurrency.
        var next = await _sequences.NextAsync(storeId, "Ticket", ct);
        return $"T-{next:D6}";
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    private static Sale ToDomain(SaleRecord record)
    {
        var lines = (JsonSerializer.Deserialize<List<SaleLineJson>>(record.LinesJson) ?? new())
            .Select(l => new SaleLine(l.ProductId, l.Quantity, l.UnitPrice, l.VatRate, l.LineSubtotal, l.LineVat, l.LineTotal, l.PromotionCode))
            .ToList();
        var tenders = (JsonSerializer.Deserialize<List<SaleTenderJson>>(record.TendersJson) ?? new())
            .Select(t => new SaleTender(t.TenderType, t.Amount, t.Reference))
            .ToList();

        // Full constructor overload preserves the original CompletedAt and
        // VeriFactuRecordId instead of stamping "now" and requiring a separate
        // AttachVeriFactuRecord call — see Sale.cs for why that distinction matters.
        return new Sale(
            record.Id, record.StoreId, record.RegisterId, record.CustomerId, record.TicketNumber,
            lines, tenders, record.IdempotencyKey,
            completedAt: record.CompletedAt, veriFactuRecordId: record.VeriFactuRecordId);
    }
}
