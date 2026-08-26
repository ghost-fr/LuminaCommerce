using Lumina.Application.Catalogue;
using Lumina.Application.Pos;
using Lumina.Application.Ports;
using Lumina.Contracts.Pos;
using Lumina.Domain.Catalogue;
using Lumina.Domain.Identity;
using Lumina.Domain.Sales;
using Xunit;

namespace Lumina.Application.Tests.Pos;

public class PosSaleServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid StoreId = Guid.NewGuid();

    private sealed class FakeCartRepository : ICartRepository
    {
        public Cart? Cart;
        public Task<Cart?> FindByIdAsync(Guid cartId, CancellationToken ct = default) => Task.FromResult(Cart);
        public Task AddAsync(Cart cart, CancellationToken ct = default) { Cart = cart; return Task.CompletedTask; }
        public Task UpdateAsync(Cart cart, CancellationToken ct = default) { Cart = cart; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeRegisterRepository : IRegisterRepository
    {
        public Register? Register;
        public Task<Register?> FindByIdAsync(Guid registerId, CancellationToken ct = default) =>
            Task.FromResult(Register?.Id == registerId ? Register : null);
    }

    private sealed class FakeSaleRepository : ISaleRepository
    {
        public readonly List<Sale> Sales = new();
        private int _ticketCounter;
        public Task AddAsync(Sale sale, CancellationToken ct = default) { Sales.Add(sale); return Task.CompletedTask; }
        public Task<Sale?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(Sales.FirstOrDefault(s => s.IdempotencyKey == idempotencyKey));
        public Task<string> NextTicketNumberAsync(Guid storeId, CancellationToken ct = default) =>
            Task.FromResult($"T-{++_ticketCounter:D6}");
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeStoreRepository : IStoreRepository
    {
        public Store? Store;
        public Task<Store?> FindByIdAsync(Guid storeId, CancellationToken ct = default) =>
            Task.FromResult(Store?.Id == storeId ? Store : null);
        public Task<bool> BelongsToTenantAsync(Guid storeId, Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Store?.Id == storeId && Store.TenantId == tenantId);
        public Task<Store?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Store?.TenantId == tenantId ? Store : null);
    }

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public Tenant? Tenant;
        public Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Tenant?.Id == tenantId ? Tenant : null);
    }

    private sealed class FakeStockChecker : IStockAvailabilityChecker
    {
        public bool Available = true;
        public Task<bool> IsAvailableAsync(Guid productId, decimal requestedQuantity, CancellationToken ct = default) =>
            Task.FromResult(Available);
    }

    private sealed class FakeFiscalGenerator : IFiscalRecordGenerator
    {
        public int CallCount;
        public Task<FiscalRecordResult> GenerateAsync(Guid sifBoundaryId, FiscalRecordRequest request, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new FiscalRecordResult(Guid.NewGuid(), "fakehash", "https://example.test/qr"));
        }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public Product? Product;
        public Task<Product?> FindByIdAsync(Guid productId, CancellationToken ct = default) =>
            Task.FromResult(Product?.Id == productId ? Product : null);
        public Task<Product?> FindByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct = default) =>
            Task.FromResult(Product?.Barcode == barcode ? Product : null);
        public Task<(IReadOnlyList<Product>, int)> SearchAsync(Guid tenantId, string? query, int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Product>, int)>((Array.Empty<Product>(), 0));
    }

    private sealed class FakeTaxCategoryRepository : ITaxCategoryRepository
    {
        public TaxCategory? TaxCategory;
        public Task<TaxCategory?> FindByIdAsync(Guid taxCategoryId, CancellationToken ct = default) =>
            Task.FromResult(TaxCategory);
    }

    private sealed class FakePromotionRepository : IPromotionRepository
    {
        public Task<IReadOnlyList<Promotion>> GetActiveForProductAsync(Guid productId, DateTimeOffset atMoment, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Promotion>>(Array.Empty<Promotion>());
    }

    private sealed class FakeTenantProvider : ICurrentTenantProvider
    {
        public Guid TenantId { get; init; }
    }

    private sealed class Fixture
    {
        public FakeCartRepository Carts = new();
        public FakeRegisterRepository Registers = new();
        public FakeSaleRepository Sales = new();
        public FakeStoreRepository Stores = new();
        public FakeTenantRepository Tenants = new();
        public FakeStockChecker Stock = new();
        public FakeFiscalGenerator Fiscal = new();
        public FakeProductRepository Products = new();
        public FakeTaxCategoryRepository TaxCategories = new();
        public FakePromotionRepository Promotions = new();

        public PosSaleService BuildService()
        {
            var catalogue = new ProductCatalogueService(Products, new PricingService(Products, TaxCategories, Promotions));
            return new PosSaleService(
                Carts, Registers, Sales, Stores, Tenants, Stock, Fiscal, catalogue, Products,
                new FakeTenantProvider { TenantId = PosSaleServiceTests.TenantId });
        }
    }

    private static Fixture SetUpHappyPath()
    {
        var f = new Fixture();
        var taxCategory = new TaxCategory(Guid.NewGuid(), TenantId, "General 21%", 0.21m);
        var product = new Product(Guid.NewGuid(), TenantId, "8412345678901", "Test Widget", taxCategory.Id, 10.00m);
        f.Products.Product = product;
        f.TaxCategories.TaxCategory = taxCategory;

        var store = new Store(StoreId, TenantId, "Main Store", "PerStore");
        f.Stores.Store = store;
        f.Tenants.Tenant = new Tenant(TenantId, "Test Tenant SL", "B12345678");

        var register = new Register(Guid.NewGuid(), StoreId, "Register 1");
        register.Open();
        f.Registers.Register = register;

        var cart = new Cart(Guid.NewGuid(), StoreId);
        f.Carts.Cart = cart;

        return f;
    }

    [Fact]
    public async Task AddLineAsync_ResolvesPriceAndAddsLine()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();

        var summary = await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 2));

        Assert.Single(summary.Lines);
        Assert.Equal(20.00m, summary.Subtotal);
    }

    [Fact]
    public async Task AddLineAsync_UnknownBarcode_ThrowsProductNotFound()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();

        await Assert.ThrowsAsync<ProductNotFoundException>(() =>
            svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("0000000000000", 1)));
    }

    [Fact]
    public async Task CompleteSaleAsync_HappyPath_ReturnsSuccessWithTicketAndQr()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        // Total = 10.00 + 21% VAT = 12.10
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null,
            new[] { new TenderLine(TenderType.Cash, 12.10m) },
            Guid.NewGuid());

        var result = await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        Assert.Equal(CompleteSaleStatus.Success, result.Status);
        Assert.NotNull(result.SaleId);
        Assert.NotNull(result.TicketNumber);
        Assert.NotNull(result.QrPayload);
        Assert.Equal(RejectionCode.None, result.RejectionCode);
        Assert.Equal(1, f.Fiscal.CallCount);
    }

    [Fact]
    public async Task CompleteSaleAsync_RegisterNotOpen_ReturnsRejected()
    {
        var f = SetUpHappyPath();
        f.Registers.Register!.Close();
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        var request = new CompleteSaleRequest(
            f.Registers.Register.Id, null, new[] { new TenderLine(TenderType.Cash, 12.10m) }, Guid.NewGuid());

        var result = await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        Assert.Equal(CompleteSaleStatus.Rejected, result.Status);
        Assert.Equal(RejectionCode.RegisterNotOpen, result.RejectionCode);
        Assert.Null(result.SaleId);
        Assert.Null(result.TicketNumber);
        Assert.Null(result.QrPayload);
        Assert.Equal(0, f.Fiscal.CallCount);
    }

    [Fact]
    public async Task CompleteSaleAsync_StockUnavailable_ReturnsRejected()
    {
        var f = SetUpHappyPath();
        f.Stock.Available = false;
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null, new[] { new TenderLine(TenderType.Cash, 12.10m) }, Guid.NewGuid());

        var result = await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        Assert.Equal(CompleteSaleStatus.Rejected, result.Status);
        Assert.Equal(RejectionCode.StockUnavailable, result.RejectionCode);
    }

    [Fact]
    public async Task CompleteSaleAsync_TenderMismatch_ReturnsRejected()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null, new[] { new TenderLine(TenderType.Cash, 5.00m) }, Guid.NewGuid());

        var result = await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        Assert.Equal(CompleteSaleStatus.Rejected, result.Status);
        Assert.Equal(RejectionCode.PaymentValidationFailed, result.RejectionCode);
    }

    [Fact]
    public async Task CompleteSaleAsync_RetriedWithSameIdempotencyKey_ReturnsOriginalResult_DoesNotCallFiscalAgain()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        var key = Guid.NewGuid();
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null, new[] { new TenderLine(TenderType.Cash, 12.10m) }, key);

        var first = await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);
        var retry = await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        Assert.Equal(CompleteSaleStatus.Success, retry.Status);
        Assert.Equal(first.SaleId, retry.SaleId);
        Assert.Equal(first.TicketNumber, retry.TicketNumber);
        Assert.Equal(1, f.Fiscal.CallCount); // NOT called a second time on replay
    }
}
