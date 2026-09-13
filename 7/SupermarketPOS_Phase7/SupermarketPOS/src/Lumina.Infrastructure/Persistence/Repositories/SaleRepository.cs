using System.Text.Json;
using Lumina.Application.Ports;
using Lumina.Domain.Sales;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

public class SaleRepository : ISaleRepository
{
    private readonly LuminaDbContext _db;
    public SaleRepository(LuminaDbContext db) => _db = db;

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
        // Sequential, gap-free within a store is a fiscal requirement (blueprint
        // §8, commercial document numbering). This implementation counts existing
        // sales for the store — correct for single-writer scenarios but NOT safe
        // under concurrent completions on multiple registers hitting the same store
        // simultaneously (a classic race: two sales both read count=41, both compute
        // "T-000042"). Needs a proper sequence (SQLite: a dedicated counter table
        // with a transaction, or a DB-native sequence on Postgres) before this goes
        // near a multi-register store in production. Flagged, not silently risked.
        var count = await _db.SaleRecords.CountAsync(s => s.StoreId == storeId, ct);
        return $"T-{count + 1:D6}";
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<Sale>> FindByStoreAndDateRangeAsync(
        Guid storeId, DateTimeOffset fromUtcInclusive, DateTimeOffset toUtcExclusive, CancellationToken ct = default)
    {
        // NEW as of Phase 7 — backs ReportingService. Plain range filter on the
        // already-UTC CompletedAt column; timezone conversion happens in
        // ReportingService before calling this, not here.
        var records = await _db.SaleRecords
            .Where(s => s.StoreId == storeId && s.CompletedAt >= fromUtcInclusive && s.CompletedAt < toUtcExclusive)
            .ToListAsync(ct);

        return records.Select(ToDomain).ToList();
    }

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
