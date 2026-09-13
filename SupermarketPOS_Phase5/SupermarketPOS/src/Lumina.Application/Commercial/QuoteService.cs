using Lumina.Application.Ports;
using Lumina.Contracts.Commercial;
using Lumina.Domain.Commercial;

namespace Lumina.Application.Commercial;

internal sealed class QuoteLineView : IQuoteLine
{
    public Guid ProductId { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal VatRate { get; init; }
    public decimal LineTotal { get; init; }
}

internal sealed class QuoteSummaryView : IQuoteSummary
{
    public Guid QuoteId { get; init; }
    public Guid CustomerId { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<IQuoteLine> Lines { get; init; } = Array.Empty<IQuoteLine>();
    public decimal Subtotal { get; init; }
    public decimal VatTotal { get; init; }
    public decimal Total { get; init; }
    public DateTimeOffset ValidUntil { get; init; }
    public Guid? ConvertedInvoiceId { get; init; }
}

/// <summary>
/// Concrete implementation of Lumina.Contracts.Commercial.IQuoteService,
/// including ConvertToInvoiceAsync — the point where a non-fiscal Quote becomes
/// a real fiscal Invoice. This is the Phase 5 equivalent of what
/// PosSaleService.CompleteSaleAsync is for Phase 3: the one method here where
/// a mistake has fiscal consequences.
/// </summary>
public sealed class QuoteService : IQuoteService
{
    private readonly IQuoteRepository _quotes;
    private readonly IInvoiceRepository _invoices;
    private readonly IStoreRepository _stores;
    private readonly ITenantRepository _tenants;
    private readonly IFiscalRecordGenerator _fiscal;

    public QuoteService(
        IQuoteRepository quotes, IInvoiceRepository invoices,
        IStoreRepository stores, ITenantRepository tenants, IFiscalRecordGenerator fiscal)
    {
        _quotes = quotes;
        _invoices = invoices;
        _stores = stores;
        _tenants = tenants;
        _fiscal = fiscal;
    }

    public async Task<IQuoteSummary> CreateAsync(CreateQuoteRequest request, CancellationToken ct = default)
    {
        var quote = new Quote(Guid.NewGuid(), request.StoreId, request.CustomerId, request.ValidUntil);
        await _quotes.AddAsync(quote, ct);
        await _quotes.SaveChangesAsync(ct);
        return ToSummary(quote);
    }

    public async Task<IQuoteSummary> AddLineAsync(Guid quoteId, AddQuoteLineRequest request, CancellationToken ct = default)
    {
        var quote = await RequireQuoteAsync(quoteId, ct);
        quote.AddLine(request.ProductId, request.Description, request.Quantity, request.UnitPrice, request.VatRate);
        await _quotes.UpdateAsync(quote, ct);
        await _quotes.SaveChangesAsync(ct);
        return ToSummary(quote);
    }

    public async Task<IQuoteSummary> RemoveLineAsync(Guid quoteId, RemoveQuoteLineRequest request, CancellationToken ct = default)
    {
        var quote = await RequireQuoteAsync(quoteId, ct);
        quote.RemoveLine(request.ProductId);
        await _quotes.UpdateAsync(quote, ct);
        await _quotes.SaveChangesAsync(ct);
        return ToSummary(quote);
    }

    public async Task<IQuoteSummary> SendAsync(Guid quoteId, CancellationToken ct = default)
    {
        var quote = await RequireQuoteAsync(quoteId, ct);
        quote.Send();
        await _quotes.UpdateAsync(quote, ct);
        await _quotes.SaveChangesAsync(ct);
        return ToSummary(quote);
    }

    public async Task<IQuoteSummary> AcceptAsync(Guid quoteId, CancellationToken ct = default)
    {
        var quote = await RequireQuoteAsync(quoteId, ct);
        quote.Accept();
        await _quotes.UpdateAsync(quote, ct);
        await _quotes.SaveChangesAsync(ct);
        return ToSummary(quote);
    }

    public async Task<IQuoteSummary> RejectAsync(Guid quoteId, CancellationToken ct = default)
    {
        var quote = await RequireQuoteAsync(quoteId, ct);
        quote.Reject();
        await _quotes.UpdateAsync(quote, ct);
        await _quotes.SaveChangesAsync(ct);
        return ToSummary(quote);
    }

    public async Task<ConvertToInvoiceResult> ConvertToInvoiceAsync(
        Guid quoteId, Guid idempotencyKey, CancellationToken ct = default)
    {
        // Idempotency check FIRST — same discipline as PosSaleService.CompleteSaleAsync.
        var existing = await _invoices.FindByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing is not null)
        {
            return new ConvertToInvoiceResult(
                ConvertToInvoiceStatus.Success, existing.Id, existing.InvoiceNumber,
                null, // QR not stored on Invoice — same known gap as Sale, see PHASE2_3_NOTES.md item 8
                null);
        }

        var quote = await RequireQuoteAsync(quoteId, ct);
        if (quote.Status != QuoteStatus.Accepted)
        {
            return new ConvertToInvoiceResult(
                ConvertToInvoiceStatus.Rejected, null, null, null,
                $"Quote must be Accepted to convert to an invoice (current status: {quote.Status}).");
        }

        var store = await _stores.FindByIdAsync(quote.StoreId, ct)
            ?? throw new InvalidOperationException($"Quote {quoteId} references Store {quote.StoreId} which does not exist.");
        var tenant = await _tenants.FindByIdAsync(store.TenantId, ct)
            ?? throw new InvalidOperationException($"Store {store.Id} references Tenant {store.TenantId} which does not exist.");

        var invoiceNumber = await _invoices.NextInvoiceNumberAsync(store.Id, ct);
        var lines = quote.Lines.Select(l => new InvoiceLine(
            l.ProductId, l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.LineSubtotal, l.LineVat, l.LineTotal)).ToList();

        var invoice = new Invoice(Guid.NewGuid(), store.Id, quote.CustomerId, invoiceNumber, lines, quote.Id, idempotencyKey);

        var storeTimeZone = TimeZoneInfo.FindSystemTimeZoneById(store.TimeZoneId);
        var generatedAtLocal = TimeZoneInfo.ConvertTime(invoice.IssuedAt, storeTimeZone);

        // TipoFactura = "F1" (factura completa) — an Invoice always has a specific
        // customer, unlike a POS Sale's "F2" (simplified ticket) default. Same
        // caveat as PosSaleService's F2: NOT independently confirmed against the
        // AEAT L2 invoice-type code list. See docs/PHASE5_NOTES.md.
        const string invoiceType = "F1";

        var fiscalResult = await _fiscal.GenerateAsync(
            store.Id,
            new FiscalRecordRequest(
                tenant.Nif, invoiceNumber, DateOnly.FromDateTime(invoice.IssuedAt.Date),
                invoiceType, invoice.VatTotal, invoice.Total, generatedAtLocal),
            ct);

        invoice.AttachVeriFactuRecord(fiscalResult.VeriFactuRecordId);

        // Same atomicity discipline as PosSaleService: mark the Quote converted
        // and add the Invoice in the SAME unit of work, committed by ONE final
        // SaveChangesAsync — a crash between the two can't leave a converted
        // quote with no matching invoice, or vice versa.
        quote.MarkConverted(invoice.Id);
        await _quotes.UpdateAsync(quote, ct);
        await _invoices.AddAsync(invoice, ct);
        await _invoices.SaveChangesAsync(ct);

        return new ConvertToInvoiceResult(
            ConvertToInvoiceStatus.Success, invoice.Id, invoice.InvoiceNumber, fiscalResult.QrPayload, null);
    }

    private async Task<Quote> RequireQuoteAsync(Guid quoteId, CancellationToken ct) =>
        await _quotes.FindByIdAsync(quoteId, ct)
        ?? throw new InvalidOperationException($"Quote {quoteId} was not found.");

    private static IQuoteSummary ToSummary(Quote quote) => new QuoteSummaryView
    {
        QuoteId = quote.Id,
        CustomerId = quote.CustomerId,
        Status = quote.Status.ToString(),
        Lines = quote.Lines.Select(l => (IQuoteLine)new QuoteLineView
        {
            ProductId = l.ProductId,
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            VatRate = l.VatRate,
            LineTotal = l.LineTotal
        }).ToList(),
        Subtotal = quote.Subtotal,
        VatTotal = quote.VatTotal,
        Total = quote.Total,
        ValidUntil = quote.ValidUntil,
        ConvertedInvoiceId = quote.ConvertedInvoiceId
    };
}
