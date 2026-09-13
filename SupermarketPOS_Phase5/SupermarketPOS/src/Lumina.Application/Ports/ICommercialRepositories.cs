using Lumina.Domain.Commercial;

namespace Lumina.Application.Ports;

public interface IQuoteRepository
{
    Task<Quote?> FindByIdAsync(Guid quoteId, CancellationToken ct = default);
    Task AddAsync(Quote quote, CancellationToken ct = default);

    /// <summary>Must be called after mutating a Quote and before SaveChangesAsync
    /// — same reasoning and same pattern as ICartRepository.UpdateAsync (Quote is
    /// a plain domain object, not EF-tracked directly; see Infrastructure's
    /// QuoteRepository for why).</summary>
    Task UpdateAsync(Quote quote, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IInvoiceRepository
{
    Task<Invoice?> FindByIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task<Invoice?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken ct = default);
    Task AddAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>Persists a Status/CancellationVeriFactuRecordId change on an
    /// already-added Invoice (the Cancel/MarkRectified path). Deliberately NOT
    /// a general UpdateAsync — Invoice's core fields (lines, totals, customer)
    /// are immutable by design, and this repository shouldn't offer a way to
    /// silently persist a change to them even by accident.</summary>
    Task UpdateStatusAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>Same gap-free-within-a-store requirement, and the same
    /// under-concurrency caveat, as ISaleRepository.NextTicketNumberAsync
    /// (Phase 2/3 notes) — not re-solved here. Invoices use their own numbering
    /// series, separate from POS ticket numbers.</summary>
    Task<string> NextInvoiceNumberAsync(Guid storeId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
