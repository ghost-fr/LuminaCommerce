using System.Data;
using System.Text.Json;
using Lumina.Application.Ports;
using Lumina.Domain.Sales;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.Data.Sqlite;
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
        // Atomic per-store sequence — see Claude Drive note CLAUDE_DEBUG_TICKET_SEQUENCE.txt
        var connection = (SqliteConnection)_db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO TicketSequences (StoreId, NextNumber) VALUES ($storeId, 2)
                    ON CONFLICT(StoreId) DO UPDATE SET NextNumber = NextNumber + 1
                    RETURNING NextNumber - 1;
                    """;
                command.Parameters.AddWithValue("$storeId", storeId.ToString());

                var result = await command.ExecuteScalarAsync(ct);
                var number = Convert.ToInt64(result);
                return $"T-{number:D6}";
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5 /* SQLITE_BUSY */ && attempt < maxAttempts)
            {
                await Task.Delay(25 * attempt, ct);
            }
        }
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

        return new Sale(
            record.Id, record.StoreId, record.RegisterId, record.CustomerId, record.TicketNumber,
            lines, tenders, record.IdempotencyKey,
            completedAt: record.CompletedAt, veriFactuRecordId: record.VeriFactuRecordId);
    }
}
