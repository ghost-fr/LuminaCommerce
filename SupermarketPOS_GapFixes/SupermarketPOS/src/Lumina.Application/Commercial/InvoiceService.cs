using Lumina.Application.Ports;
using Lumina.Contracts.Commercial;
using Lumina.Domain.Commercial;

namespace Lumina.Application.Commercial;

internal sealed class InvoiceSummaryView : IInvoiceSummary
{
    public Guid InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public decimal Total { get; init; }
}

/// <summary>
/// Concrete implementation of Lumina.Contracts.Commercial.IInvoiceService.
/// CancelAsync is the first real caller of IFiscalRecordGenerator.GenerateCancellationAsync
/// — the cancellation hash-chain mechanism was built and verified during the
/// hash-chain fix but had no Application-layer caller until this.
/// </summary>
public sealed class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IStoreRepository _stores;
    private readonly ITenantRepository _tenants;
    private readonly IFiscalRecordGenerator _fiscal;

    public InvoiceService(
        IInvoiceRepository invoices, IStoreRepository stores, ITenantRepository tenants, IFiscalRecordGenerator fiscal)
    {
        _invoices = invoices;
        _stores = stores;
        _tenants = tenants;
        _fiscal = fiscal;
    }

    public async Task<IInvoiceSummary?> FindByIdAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _invoices.FindByIdAsync(invoiceId, ct);
        return invoice is null ? null : ToSummary(invoice);
    }

    public async Task<CancelInvoiceResult> CancelAsync(Guid invoiceId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return new CancelInvoiceResult(CancelInvoiceStatus.Rejected, "A cancellation reason is required.");

        var invoice = await _invoices.FindByIdAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} was not found.");

        if (invoice.Status != InvoiceStatus.Issued)
        {
            return new CancelInvoiceResult(
                CancelInvoiceStatus.Rejected,
                $"Only an Issued invoice can be cancelled (current status: {invoice.Status}).");
        }

        var store = await _stores.FindByIdAsync(invoice.StoreId, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} references Store {invoice.StoreId} which does not exist.");
        var tenant = await _tenants.FindByIdAsync(store.TenantId, ct)
            ?? throw new InvalidOperationException($"Store {store.Id} references Tenant {store.TenantId} which does not exist.");

        var storeTimeZone = TimeZoneInfo.FindSystemTimeZoneById(store.TimeZoneId);
        var generatedAtLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, storeTimeZone);

        // NOTE: `reason` is captured in the CancelInvoiceResult/UI flow but is NOT
        // currently part of FiscalCancellationRequest — AEAT's Caso 3 field set
        // (confirmed) has no free-text reason field in the hashed record itself.
        // If a reason needs to be fiscally recorded, it likely belongs in a
        // different part of the XML submission (outside the hash), not invented
        // here. Reason is still required and validated above for audit-trail
        // purposes at the Lumina level even though it doesn't flow into the hash.
        FiscalCancellationResult cancellationResult;
        try
        {
            cancellationResult = await _fiscal.GenerateCancellationAsync(
                store.Id,
                new FiscalCancellationRequest(
                    tenant.Nif, invoice.InvoiceNumber, DateOnly.FromDateTime(invoice.IssuedAt.Date), generatedAtLocal),
                ct);
        }
        catch (FiscalChainIntegrityException ex)
        {
            // Same chain-integrity pre-check as PosSaleService/QuoteService.
            // Nothing was persisted — the Invoice hasn't been marked cancelled.
            return new CancelInvoiceResult(CancelInvoiceStatus.Rejected, ex.Message);
        }

        invoice.Cancel(cancellationResult.VeriFactuRecordId);
        await _invoices.UpdateStatusAsync(invoice, ct);
        await _invoices.SaveChangesAsync(ct);

        return new CancelInvoiceResult(CancelInvoiceStatus.Success, null);
    }

    private static IInvoiceSummary ToSummary(Invoice invoice) => new InvoiceSummaryView
    {
        InvoiceId = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        Status = invoice.Status.ToString(),
        CustomerId = invoice.CustomerId,
        Total = invoice.Total
    };
}
