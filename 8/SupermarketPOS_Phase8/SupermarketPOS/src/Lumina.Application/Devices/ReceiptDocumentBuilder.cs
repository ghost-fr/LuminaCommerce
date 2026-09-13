using System.Globalization;
using Lumina.Application.Ports;
using Lumina.Contracts.Devices;
using Lumina.Domain.Identity;
using Lumina.Domain.Sales;

namespace Lumina.Application.Devices;

/// <summary>
/// Builds a printable ReceiptDocument from a real, completed Sale — this is
/// the piece that ties real domain data to the (currently emulated) device
/// port, so the "device round-trip" this phase delivers is a genuine sale's
/// data flowing through to a printer, not a hand-typed test fixture. UI is
/// free to build its own on-screen ticket preview independently (as already
/// shown working in the POS mockup) — this builder is specifically for what
/// gets sent to IReceiptPrinter, a different concern with different
/// formatting needs (fixed-width text, not XAML).
/// </summary>
public sealed class ReceiptDocumentBuilder
{
    private readonly IProductRepository _products;

    public ReceiptDocumentBuilder(IProductRepository products) => _products = products;

    public async Task<ReceiptDocument> BuildForSaleAsync(
        Sale sale, Tenant tenant, Store store, string qrPayload, CancellationToken ct = default)
    {
        var elements = new List<ReceiptElement>
        {
            new ReceiptTextLine(store.Name, ReceiptLineAlignment.Center, Bold: true),
            new ReceiptTextLine($"NIF: {tenant.Nif}", ReceiptLineAlignment.Center),
            new ReceiptSeparator(),
            new ReceiptTextLine($"Ticket: {sale.TicketNumber}"),
            new ReceiptTextLine(sale.CompletedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
            new ReceiptSeparator()
        };

        foreach (var line in sale.Lines)
        {
            var product = await _products.FindByIdAsync(line.ProductId, ct);
            var name = product?.Name ?? "(unknown product)";
            elements.Add(new ReceiptTextLine(name));
            elements.Add(new ReceiptTextLine(
                $"{Fmt(line.Quantity)} x {Fmt(line.UnitPrice)} = {Fmt(line.LineTotal)}", ReceiptLineAlignment.Right));
        }

        elements.Add(new ReceiptSeparator());
        elements.Add(new ReceiptTextLine($"Subtotal: {Fmt(sale.Subtotal)}", ReceiptLineAlignment.Right));
        elements.Add(new ReceiptTextLine($"IVA: {Fmt(sale.VatTotal)}", ReceiptLineAlignment.Right));
        elements.Add(new ReceiptTextLine($"TOTAL: {Fmt(sale.Total)}", ReceiptLineAlignment.Right, Bold: true));
        elements.Add(new ReceiptSeparator());

        foreach (var tender in sale.Tenders)
            elements.Add(new ReceiptTextLine($"{tender.TenderType}: {Fmt(tender.Amount)}", ReceiptLineAlignment.Right));

        elements.Add(new ReceiptQrCode(qrPayload));
        elements.Add(new ReceiptTextLine("Documento verificable en aeat.es", ReceiptLineAlignment.Center));
        elements.Add(new ReceiptCut());

        return new ReceiptDocument(elements);
    }

    private static string Fmt(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);
}
