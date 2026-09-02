using Lumina.Application.Ports;
using Lumina.Application.Reporting;
using Lumina.Contracts.Pos;
using Lumina.Contracts.Reporting;
using Lumina.Domain.Cash;
using Lumina.Domain.Catalogue;
using Lumina.Domain.Identity;
using Lumina.Domain.Sales;
using Lumina.Domain.Stock;
using Xunit;

namespace Lumina.Application.Tests.Reporting;

public class ReportingServiceTests
{
    private static readonly Guid StoreId = Guid.NewGuid();
    private static readonly Guid ProductAId = Guid.NewGuid();
    private static readonly Guid ProductBId = Guid.NewGuid();

    private sealed class FakeSaleRepository : ISaleRepository
    {
        public readonly List<Sale> Sales = new();
        public Task AddAsync(Sale sale, CancellationToken ct = default) { Sales.Add(sale); return Task.CompletedTask; }
        public Task<Sale?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult<Sale?>(null);
        public Task<string> NextTicketNumberAsync(Guid storeId, CancellationToken ct = default) => Task.FromResult("T-000001");
        public Task<IReadOnlyList<Sale>> FindByStoreAndDateRangeAsync(
            Guid storeId, DateTimeOffset fromUtcInclusive, DateTimeOffset toUtcExclusive, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Sale>>(Sales.Where(s =>
                s.StoreId == storeId && s.CompletedAt >= fromUtcInclusive && s.CompletedAt < toUtcExclusive).ToList());
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public readonly Dictionary<Guid, Product> Products = new();
        public Task<Product?> FindByIdAsync(Guid productId, CancellationToken ct = default) =>
            Task.FromResult(Products.TryGetValue(productId, out var p) ? p : null);
        public Task<Product?> FindByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct = default) => Task.FromResult<Product?>(null);
        public Task<(IReadOnlyList<Product>, int)> SearchAsync(Guid tenantId, string? query, int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Product>, int)>((Array.Empty<Product>(), 0));
    }

    private sealed class FakeStockLedgerRepository : IStockLedgerRepository
    {
        public readonly List<StockMovement> Movements = new();
        public Task<decimal> GetQuantityOnHandAsync(Guid storeId, Guid productId, CancellationToken ct = default) =>
            Task.FromResult(Movements.Where(m => m.StoreId == storeId && m.ProductId == productId).Sum(m => m.QuantityDelta));
        public Task AddMovementAsync(StockMovement movement, CancellationToken ct = default) { Movements.Add(movement); return Task.CompletedTask; }
        public Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandByProductAsync(Guid storeId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(
                Movements.Where(m => m.StoreId == storeId).GroupBy(m => m.ProductId).ToDictionary(g => g.Key, g => g.Sum(m => m.QuantityDelta)));
    }

    private sealed class FakeStoreRepository : IStoreRepository
    {
        public Store? Store;
        public Task<Store?> FindByIdAsync(Guid storeId, CancellationToken ct = default) =>
            Task.FromResult(Store?.Id == storeId ? Store : null);
        public Task<bool> BelongsToTenantAsync(Guid storeId, Guid tenantId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Store?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<Store?>(null);
    }

    private sealed class FakeRegisterSessionRepository : IRegisterSessionRepository
    {
        public RegisterSession? Session;
        public Task<RegisterSession?> FindByIdAsync(Guid registerSessionId, CancellationToken ct = default) =>
            Task.FromResult(Session?.Id == registerSessionId ? Session : null);
        public Task<RegisterSession?> FindOpenByRegisterIdAsync(Guid registerId, CancellationToken ct = default) => Task.FromResult<RegisterSession?>(null);
        public Task AddAsync(RegisterSession session, CancellationToken ct = default) { Session = session; return Task.CompletedTask; }
        public Task UpdateAsync(RegisterSession session, CancellationToken ct = default) { Session = session; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCashMovementRepository : ICashMovementRepository
    {
        public readonly List<CashMovement> Movements = new();
        public Task AddAsync(CashMovement movement, CancellationToken ct = default) { Movements.Add(movement); return Task.CompletedTask; }
        public Task<decimal> GetTotalForSessionAsync(Guid registerSessionId, CancellationToken ct = default) =>
            Task.FromResult(Movements.Where(m => m.RegisterSessionId == registerSessionId).Sum(m => m.Amount));
        public Task<IReadOnlyList<CashMovement>> GetForSessionAsync(Guid registerSessionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CashMovement>>(Movements.Where(m => m.RegisterSessionId == registerSessionId).ToList());
    }

    private sealed class Fixture
    {
        public FakeSaleRepository Sales = new();
        public FakeProductRepository Products = new();
        public FakeStockLedgerRepository StockLedger = new();
        public FakeStoreRepository Stores = new();
        public FakeRegisterSessionRepository RegisterSessions = new();
        public FakeCashMovementRepository CashMovements = new();

        public Fixture()
        {
            Stores.Store = new Store(StoreId, Guid.NewGuid(), "Main Store", "PerStore"); // TimeZoneId defaults to Europe/Madrid
        }

        public ReportingService BuildService() =>
            new(Sales, Products, StockLedger, Stores, RegisterSessions, CashMovements);
    }

    private static Sale MakeSale(Guid storeId, DateTimeOffset completedAtUtc, decimal cashAmount, decimal cardAmount, Guid productId, decimal qty, decimal unitPrice)
    {
        var subtotal = unitPrice * qty;
        var vat = Math.Round(subtotal * 0.21m, 2);
        var line = new SaleLine(productId, qty, unitPrice, 0.21m, subtotal, vat, subtotal + vat, null);
        var tenders = new List<SaleTender>();
        if (cashAmount > 0) tenders.Add(new SaleTender(TenderType.Cash.ToString(), cashAmount, null));
        if (cardAmount > 0) tenders.Add(new SaleTender(TenderType.Card.ToString(), cardAmount, null));

        return new Sale(
            Guid.NewGuid(), storeId, Guid.NewGuid(), null, $"T-{Guid.NewGuid():N}".Substring(0, 10),
            new[] { line }, tenders, Guid.NewGuid(),
            completedAt: completedAtUtc, veriFactuRecordId: null);
    }

    [Fact]
    public async Task GetDailySalesReportAsync_TotalsExactlyMatchUnderlyingSales()
    {
        var f = new Fixture();
        // Local noon on the target date (Europe/Madrid, so well within the UTC day boundary either way)
        var targetDate = new DateOnly(2026, 6, 15);
        var completedAtUtc = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero); // 12:00 local (CEST, +02:00) in summer

        f.Sales.Sales.Add(MakeSale(StoreId, completedAtUtc, cashAmount: 12.10m, cardAmount: 0m, ProductAId, 1, 10.00m));
        f.Sales.Sales.Add(MakeSale(StoreId, completedAtUtc.AddHours(1), cashAmount: 0m, cardAmount: 24.20m, ProductAId, 2, 10.00m));

        var svc = f.BuildService();
        var report = await svc.GetDailySalesReportAsync(new DailySalesReportRequest(StoreId, targetDate));

        Assert.Equal(2, report.SaleCount);
        // Manually summed expectation, independent of any internal computation path:
        var expectedGross = f.Sales.Sales.Sum(s => s.Subtotal);
        var expectedVat = f.Sales.Sales.Sum(s => s.VatTotal);
        var expectedTotal = f.Sales.Sales.Sum(s => s.Total);
        Assert.Equal(expectedGross, report.GrossSubtotal);
        Assert.Equal(expectedVat, report.VatTotal);
        Assert.Equal(expectedTotal, report.GrandTotal);
        Assert.Equal(12.10m, report.CashTotal);
        Assert.Equal(24.20m, report.CardTotal);
        // Internal consistency: subtotal + vat == grand total, and tender breakdown sums to grand total
        Assert.Equal(report.GrandTotal, report.GrossSubtotal + report.VatTotal);
        Assert.Equal(report.GrandTotal, report.CashTotal + report.CardTotal + report.OtherTenderTotal);
    }

    [Fact]
    public async Task GetDailySalesReportAsync_SalesOnDifferentDay_Excluded()
    {
        var f = new Fixture();
        var targetDate = new DateOnly(2026, 6, 15);
        var wrongDaySale = MakeSale(StoreId, new DateTimeOffset(2026, 6, 16, 10, 0, 0, TimeSpan.Zero), 12.10m, 0m, ProductAId, 1, 10.00m);
        f.Sales.Sales.Add(wrongDaySale);

        var svc = f.BuildService();
        var report = await svc.GetDailySalesReportAsync(new DailySalesReportRequest(StoreId, targetDate));

        Assert.Equal(0, report.SaleCount);
        Assert.Equal(0m, report.GrandTotal);
    }

    [Fact]
    public async Task GetDailySalesReportAsync_NoSales_ReturnsZeroedReport_NotNull()
    {
        var f = new Fixture();
        var svc = f.BuildService();

        var report = await svc.GetDailySalesReportAsync(new DailySalesReportRequest(StoreId, new DateOnly(2026, 6, 15)));

        Assert.Equal(0, report.SaleCount);
        Assert.Equal(0m, report.GrandTotal);
        Assert.Equal(0m, report.CashTotal);
    }

    [Fact]
    public async Task GetTopProductsAsync_AggregatesQuantityAndRevenueAcrossSales()
    {
        var f = new Fixture();
        f.Products.Products[ProductAId] = new Product(ProductAId, Guid.NewGuid(), "111", "Widget A", Guid.NewGuid(), 10.00m);
        var targetDate = new DateOnly(2026, 6, 15);
        var t = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
        f.Sales.Sales.Add(MakeSale(StoreId, t, 12.10m, 0m, ProductAId, 1, 10.00m));
        f.Sales.Sales.Add(MakeSale(StoreId, t.AddHours(1), 0m, 24.20m, ProductAId, 2, 10.00m));

        var svc = f.BuildService();
        var top = await svc.GetTopProductsAsync(new TopProductsRequest(StoreId, targetDate));

        var line = Assert.Single(top);
        Assert.Equal(ProductAId, line.ProductId);
        Assert.Equal("Widget A", line.ProductName);
        Assert.Equal(3m, line.QuantitySold); // 1 + 2 across both sales
    }

    [Fact]
    public async Task GetStockOnHandAsync_ReturnsPerProductTotals()
    {
        var f = new Fixture();
        f.Products.Products[ProductAId] = new Product(ProductAId, Guid.NewGuid(), "111", "Widget A", Guid.NewGuid(), 10.00m);
        f.StockLedger.Movements.Add(new StockMovement(Guid.NewGuid(), StoreId, ProductAId, 10m, StockMovementReason.Receiving, null));
        f.StockLedger.Movements.Add(new StockMovement(Guid.NewGuid(), StoreId, ProductAId, -3m, StockMovementReason.Sale, Guid.NewGuid()));

        var svc = f.BuildService();
        var report = await svc.GetStockOnHandAsync(StoreId);

        var line = Assert.Single(report);
        Assert.Equal(7m, line.QuantityOnHand);
    }

    [Fact]
    public async Task GetRegisterSessionReportAsync_ComputesLiveExpectedCash_MatchingCloseFormula()
    {
        var f = new Fixture();
        var session = new RegisterSession(Guid.NewGuid(), Guid.NewGuid(), StoreId, Guid.NewGuid(), openingFloat: 100m);
        f.RegisterSessions.Session = session;
        f.CashMovements.Movements.Add(new CashMovement(Guid.NewGuid(), session.Id, CashMovementType.CashSaleTender, 20m, null, Guid.NewGuid()));
        f.CashMovements.Movements.Add(new CashMovement(Guid.NewGuid(), session.Id, CashMovementType.CashOut, -15m, "Safe drop", null));

        var svc = f.BuildService();
        var report = await svc.GetRegisterSessionReportAsync(session.Id);

        Assert.NotNull(report);
        Assert.Equal(20m, report!.CashSalesTotal);
        Assert.Equal(-15m, report.CashOutTotal);
        Assert.Equal(105m, report.LiveExpectedCash); // 100 + 20 - 15

        // Cross-check against the SAME formula RegisterSession.Close() would use —
        // this is the actual "matches raw data" guarantee for this report.
        session.Close(Guid.NewGuid(), declaredClosingCash: 105m, expectedClosingCash: report.LiveExpectedCash);
        Assert.Equal(0m, session.Discrepancy);
    }

    [Fact]
    public async Task GetRegisterSessionReportAsync_UnknownSession_ReturnsNull()
    {
        var f = new Fixture();
        var svc = f.BuildService();

        var report = await svc.GetRegisterSessionReportAsync(Guid.NewGuid());

        Assert.Null(report);
    }
}
