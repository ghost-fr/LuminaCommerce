using System.Text.Json;
using Lumina.Application.Ports;
using Lumina.Domain.Commercial;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

public class QuoteRepository : IQuoteRepository
{
    private readonly LuminaDbContext _db;
    public QuoteRepository(LuminaDbContext db) => _db = db;

    public async Task<Quote?> FindByIdAsync(Guid quoteId, CancellationToken ct = default)
    {
        var record = await _db.QuoteRecords.FirstOrDefaultAsync(q => q.Id == quoteId, ct);
        return record is null ? null : ToDomain(record);
    }

    public async Task AddAsync(Quote quote, CancellationToken ct = default)
    {
        await _db.QuoteRecords.AddAsync(ToRecord(quote), ct);
    }

    public async Task UpdateAsync(Quote quote, CancellationToken ct = default)
    {
        var record = await _db.QuoteRecords.FirstOrDefaultAsync(q => q.Id == quote.Id, ct)
            ?? throw new InvalidOperationException($"Cannot update Quote {quote.Id}: no persisted record found. Call AddAsync first.");

        record.Status = quote.Status.ToString();
        record.ConvertedInvoiceId = quote.ConvertedInvoiceId;
        record.LinesJson = SerializeLines(quote);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    private static string SerializeLines(Quote quote) =>
        JsonSerializer.Serialize(quote.Lines.Select(l =>
            new QuoteLineJson(l.ProductId, l.Description, l.Quantity, l.UnitPrice, l.VatRate)));

    private static QuoteRecord ToRecord(Quote quote) => new()
    {
        Id = quote.Id,
        StoreId = quote.StoreId,
        CustomerId = quote.CustomerId,
        Status = quote.Status.ToString(),
        CreatedAt = quote.CreatedAt,
        ValidUntil = quote.ValidUntil,
        ConvertedInvoiceId = quote.ConvertedInvoiceId,
        LinesJson = SerializeLines(quote)
    };

    private static Quote ToDomain(QuoteRecord record)
    {
        var quote = new Quote(record.Id, record.StoreId, record.CustomerId, record.ValidUntil, record.CreatedAt);

        var lines = JsonSerializer.Deserialize<List<QuoteLineJson>>(record.LinesJson) ?? new();
        foreach (var line in lines)
            quote.AddLine(line.ProductId, line.Description, line.Quantity, line.UnitPrice, line.VatRate);

        // Replay the state machine to reach the persisted status — Quote has no
        // "set status directly" escape hatch (deliberately, so invariants always
        // run), so rehydration drives the same transitions the original flow did.
        switch (Enum.Parse<QuoteStatus>(record.Status))
        {
            case QuoteStatus.Sent: quote.Send(); break;
            case QuoteStatus.Accepted: quote.Send(); quote.Accept(); break;
            case QuoteStatus.Rejected: quote.Send(); quote.Reject(); break;
            case QuoteStatus.Expired: quote.Send(); quote.MarkExpired(); break;
            case QuoteStatus.ConvertedToInvoice:
                quote.Send();
                quote.Accept();
                quote.MarkConverted(record.ConvertedInvoiceId!.Value);
                break;
            case QuoteStatus.Draft:
            default:
                break; // already Draft from construction
        }

        return quote;
    }
}
