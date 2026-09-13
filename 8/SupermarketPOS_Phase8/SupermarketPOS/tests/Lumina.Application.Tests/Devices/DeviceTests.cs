using Lumina.Application.Devices;
using Lumina.Application.Ports;
using Lumina.Contracts.Devices;
using Lumina.Domain.Catalogue;
using Lumina.Domain.Identity;
using Lumina.Domain.Sales;
using Xunit;

namespace Lumina.Application.Tests.Devices;

public class SimulatedReceiptPrinterTests
{
    [Fact]
    public async Task PrintAsync_Succeeds_AndRecordsTheDocument()
    {
        var printer = new SimulatedReceiptPrinter();
        var doc = new ReceiptDocument(new ReceiptElement[] { new ReceiptTextLine("Hello") });

        var result = await printer.PrintAsync(doc);

        Assert.Equal(PrintStatus.Success, result.Status);
        Assert.Single(printer.PrintedReceipts);
    }

    [Fact]
    public async Task GetStatusAsync_AlwaysConnected()
    {
        var printer = new SimulatedReceiptPrinter();
        Assert.Equal(DeviceConnectionStatus.Connected, await printer.GetStatusAsync());
    }

    [Fact]
    public void RenderAsPlainText_IncludesAllTextLines()
    {
        var doc = new ReceiptDocument(new ReceiptElement[]
        {
            new ReceiptTextLine("Store Name", ReceiptLineAlignment.Center, Bold: true),
            new ReceiptSeparator(),
            new ReceiptTextLine("Item x1"),
            new ReceiptQrCode("https://example.test/qr"),
            new ReceiptCut()
        });

        var rendered = SimulatedReceiptPrinter.RenderAsPlainText(doc);

        Assert.Contains("STORE NAME", rendered); // bold => uppercased in the plain-text render
        Assert.Contains("Item x1", rendered);
        Assert.Contains("[QR: https://example.test/qr]", rendered);
        Assert.Contains("CUT", rendered);
    }
}

public class SimulatedScaleServiceTests
{
    [Fact]
    public async Task ReadWeightAsync_ReturnsPlausibleStableWeight()
    {
        var scale = new SimulatedScaleService();

        var reading = await scale.ReadWeightAsync();

        Assert.NotNull(reading);
        Assert.True(reading!.Kilograms > 0m);
        Assert.True(reading.IsStable);
    }

    [Fact]
    public async Task GetStatusAsync_AlwaysConnected()
    {
        var scale = new SimulatedScaleService();
        Assert.Equal(DeviceConnectionStatus.Connected, await scale.GetStatusAsync());
    }
}

/// <summary>
/// The actual "device round-trip" test the Phase 8 gate asks for: a REAL Sale
/// (built the same way PosSaleService would construct one, with real
/// products/tenders) flows through ReceiptDocumentBuilder into an EMULATED
/// printer — end to end, with assertions on the actual printed content, not
/// just "it didn't throw."
/// </summary>
public class ReceiptRoundTripTests
{
    private sealed class FakeProductRepository : IProductRepository
    {
        public readonly Dictionary<Guid, Product> Products = new();
        public Task<Product?> FindByIdAsync(Guid productId, CancellationToken ct = default) =>
            Task.FromResult(Products.TryGetValue(productId, out var p) ? p : null);
        public Task<Product?> FindByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct = default) => Task.FromResult<Product?>(null);
        public Task<(IReadOnlyList<Product>, int)> SearchAsync(Guid tenantId, string? query, int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Product>, int)>((Array.Empty<Product>(), 0));
    }

    [Fact]
    public async Task RealSale_ThroughBuilder_ToEmulatedPrinter_ProducesCorrectContent()
    {
        var productId = Guid.NewGuid();
        var products = new FakeProductRepository();
        products.Products[productId] = new Product(productId, Guid.NewGuid(), "8412345678901", "Leche Entera 1L", Guid.NewGuid(), 1.29m);

        var line = new SaleLine(productId, 2m, 1.29m, 0.10m, 2.58m, 0.26m, 2.84m, null);
        var sale = new Sale(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "T-000042",
            new[] { line }, new[] { new SaleTender("Cash", 2.84m, null) }, Guid.NewGuid());

        var tenant = new Tenant(Guid.NewGuid(), "Tienda Central SL", "B12345678");
        var store = new Store(Guid.NewGuid(), tenant.Id, "Tienda Central", "PerStore");
        var qrPayload = "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR?nif=B12345678&numserie=T-000042&fecha=15-01-2026&importe=2.84";

        var builder = new ReceiptDocumentBuilder(products);
        var document = await builder.BuildForSaleAsync(sale, tenant, store, qrPayload);

        var printer = new SimulatedReceiptPrinter();
        var printResult = await printer.PrintAsync(document);

        Assert.Equal(PrintStatus.Success, printResult.Status);
        Assert.Single(printer.PrintedReceipts);

        var rendered = SimulatedReceiptPrinter.RenderAsPlainText(document);
        Assert.Contains("Leche Entera 1L", rendered);
        Assert.Contains("T-000042", rendered);
        Assert.Contains("2.84", rendered); // total
        Assert.Contains($"[QR: {qrPayload}]", rendered);
        Assert.Contains("B12345678", rendered); // NIF
    }
}
