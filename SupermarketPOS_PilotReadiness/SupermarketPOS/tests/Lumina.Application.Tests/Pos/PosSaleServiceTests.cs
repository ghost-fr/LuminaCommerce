using Lumina.Application.Catalogue;
using Lumina.Application.Cash;
using Lumina.Application.Pos;
using Lumina.Application.Ports;
using Lumina.Application.Stock;
using Lumina.Contracts.Pos;
using Lumina.Domain.Cash;
using Lumina.Domain.Catalogue;
using Lumina.Domain.Identity;
using Lumina.Domain.Sales;
using Lumina.Domain.Stock;
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
        public Task UpdateAsync(Register register, CancellationToken ct = default) { Register = register; return Task.CompletedTask; }
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
        public Task<IReadOnlyList<Sale>> FindByStoreAndDateRangeAsync(
            Guid storeId, DateTimeOffset fromUtcInclusive, DateTimeOffset toUtcExclusive, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Sale>>(Sales.Where(s =>
                s.StoreId == storeId && s.CompletedAt >= fromUtcInclusive && s.CompletedAt < toUtcExclusive).ToList());
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
        public Task<bool> IsAvailableAsync(Guid storeId, Guid productId, decimal requestedQuantity, CancellationToken ct = default) =>
            Task.FromResult(Available);
    }

    private sealed class FakeStockLedgerRepository : IStockLedgerRepository
    {
        public readonly List<StockMovement> Movements = new();
        public Task<decimal> GetQuantityOnHandAsync(Guid storeId, Guid productId, CancellationToken ct = default) =>
            Task.FromResult(Movements.Where(m => m.StoreId == storeId && m.ProductId == productId).Sum(m => m.QuantityDelta));
        public Task AddMovementAsync(StockMovement movement, CancellationToken ct = default)
        {
            Movements.Add(movement);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandByProductAsync(Guid storeId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(
                Movements.Where(m => m.StoreId == storeId)
                    .GroupBy(m => m.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(m => m.QuantityDelta)));
    }

    private sealed class FakeFiscalGenerator : IFiscalRecordGenerator
    {
        public int CallCount;
        public int CancellationCallCount;
        public bool ThrowChainIntegrityException;
        public Task<FiscalRecordResult> GenerateAsync(Guid sifBoundaryId, FiscalRecordRequest request, CancellationToken ct = default)
        {
            if (ThrowChainIntegrityException)
                throw new FiscalChainIntegrityException("Simulated chain integrity failure for testing.");
            CallCount++;
            return Task.FromResult(new FiscalRecordResult(Guid.NewGuid(), "fakehash", "https://example.test/qr"));
        }
        public Task<FiscalCancellationResult> GenerateCancellationAsync(Guid sifBoundaryId, FiscalCancellationRequest request, CancellationToken ct = default)
        {
            CancellationCallCount++;
            return Task.FromResult(new FiscalCancellationResult(Guid.NewGuid(), "fakecancellationhash"));
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

    private sealed class FakeRegisterSessionRepository : IRegisterSessionRepository
    {
        public RegisterSession? Session;
        public Task<RegisterSession?> FindByIdAsync(Guid registerSessionId, CancellationToken ct = default) =>
            Task.FromResult(Session?.Id == registerSessionId ? Session : null);
        public Task<RegisterSession?> FindOpenByRegisterIdAsync(Guid registerId, CancellationToken ct = default) =>
            Task.FromResult(Session?.RegisterId == registerId && Session.Status == RegisterSessionStatus.Open ? Session : null);
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

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int BeginCount;
        public int CommitCount;
        public int RollbackCount;
        public Task BeginAsync(CancellationToken ct = default) { BeginCount++; return Task.CompletedTask; }
        public Task CommitAsync(CancellationToken ct = default) { CommitCount++; return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken ct = default) { RollbackCount++; return Task.CompletedTask; }
    }

    private sealed class Fixture
    {
        public FakeCartRepository Carts = new();
        public FakeRegisterRepository Registers = new();
        public FakeSaleRepository Sales = new();
        public FakeStoreRepository Stores = new();
        public FakeTenantRepository Tenants = new();
        public FakeStockChecker Stock = new();
        public FakeStockLedgerRepository StockLedgerRepo = new();
        public FakeFiscalGenerator Fiscal = new();
        public FakeProductRepository Products = new();
        public FakeTaxCategoryRepository TaxCategories = new();
        public FakePromotionRepository Promotions = new();
        public FakeRegisterSessionRepository RegisterSessions = new();
        public FakeCashMovementRepository CashMovements = new();
        public FakeUnitOfWork UnitOfWork = new();

        public PosSaleService BuildService()
        {
            var tenantProvider = new FakeTenantProvider { TenantId = PosSaleServiceTests.TenantId };
            var catalogue = new ProductCatalogueService(
                Products, new PricingService(Products, TaxCategories, Promotions), tenantProvider);
            var stockLedger = new StockLedgerService(StockLedgerRepo);
            var cashDrawer = new CashDrawerService(RegisterSessions, CashMovements);
            return new PosSaleService(
                Carts, Registers, Sales, Stores, Tenants, Stock, Fiscal, catalogue, Products, stockLedger,
                cashDrawer, UnitOfWork);
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

        // Open RegisterSession, matching Register.IsOpen — required since
        // Phase 6, CompleteSaleAsync records cash tenders against the register's
        // open session. In real code these are always opened together via
        // RegisterSessionService.OpenAsync; tests construct them separately
        // since PosSaleServiceTests doesn't exercise RegisterSessionService itself.
        f.RegisterSessions.Session = new RegisterSession(
            Guid.NewGuid(), register.Id, StoreId, openedByUserId: Guid.NewGuid(), openingFloat: 100.00m);

        var cart = new Cart(Guid.NewGuid(), StoreId);
        f.Carts.Cart = cart;

        return f;
    }

    [Fact]
    public async Task CreateCartAsync_ReturnsSummaryWithRealCartId()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();

        var summary = await svc.CreateCartAsync(new CreateCartRequest(StoreId));

        Assert.NotEqual(Guid.Empty, summary.CartId);
        Assert.Empty(summary.Lines);
        Assert.Equal(0m, summary.Total);
        Assert.Null(summary.CustomerId);
    }

    [Fact]
    public async Task CreateCartAsync_WithCustomerId_SetsCustomerOnSummary()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();
        var customerId = Guid.NewGuid();

        var summary = await svc.CreateCartAsync(new CreateCartRequest(StoreId, customerId));

        Assert.Equal(customerId, summary.CustomerId);
    }

    [Fact]
    public async Task CreateCartAsync_ThenAddLine_UsesTheReturnedCartId()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();

        var created = await svc.CreateCartAsync(new CreateCartRequest(StoreId));
        var afterAdd = await svc.AddLineAsync(created.CartId, new AddLineRequest("8412345678901", 1));

        Assert.Equal(created.CartId, afterAdd.CartId);
        Assert.Single(afterAdd.Lines);
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
    public async Task CompleteSaleAsync_HappyPath_CommitsTransaction_NeverRollsBack()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null, new[] { new TenderLine(TenderType.Cash, 12.10m) }, Guid.NewGuid());

        await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        Assert.Equal(1, f.UnitOfWork.BeginCount);
        Assert.Equal(1, f.UnitOfWork.CommitCount);
        Assert.Equal(0, f.UnitOfWork.RollbackCount);
    }

    [Fact]
    public async Task CompleteSaleAsync_StockUnavailable_RollsBackTransaction_NeverCommits()
    {
        var f = SetUpHappyPath();
        f.Stock.Available = false;
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null, new[] { new TenderLine(TenderType.Cash, 12.10m) }, Guid.NewGuid());

        var result = await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        Assert.Equal(CompleteSaleStatus.Rejected, result.Status);
        Assert.Equal(1, f.UnitOfWork.BeginCount);
        Assert.Equal(0, f.UnitOfWork.CommitCount);
        Assert.Equal(1, f.UnitOfWork.RollbackCount);
    }

    [Fact]
    public async Task CompleteSaleAsync_FiscalChainIntegrityFailure_ReturnsFiscalChainErrorRejection_RollsBack()
    {
        var f = SetUpHappyPath();
        f.Fiscal.ThrowChainIntegrityException = true;
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null, new[] { new TenderLine(TenderType.Cash, 12.10m) }, Guid.NewGuid());

        var result = await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        Assert.Equal(CompleteSaleStatus.Rejected, result.Status);
        Assert.Equal(RejectionCode.FiscalChainError, result.RejectionCode);
        Assert.Equal(1, f.UnitOfWork.RollbackCount);
        Assert.Equal(0, f.UnitOfWork.CommitCount);
    }

    [Fact]
    public async Task CompleteSaleAsync_HappyPath_RecordsStockMovement()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 2));
        var productId = f.Products.Product!.Id;
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null, new[] { new TenderLine(TenderType.Cash, 24.20m) }, Guid.NewGuid());

        await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        var onHand = await f.StockLedgerRepo.GetQuantityOnHandAsync(StoreId, productId);
        Assert.Equal(-2m, onHand); // no prior stock in this fixture, so 2 sold = -2 on hand
        Assert.Single(f.StockLedgerRepo.Movements);
        Assert.Equal(StockMovementReason.Sale, f.StockLedgerRepo.Movements[0].Reason);
    }

    [Fact]
    public async Task CompleteSaleAsync_CashTender_RecordsCashMovement()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null, new[] { new TenderLine(TenderType.Cash, 12.10m) }, Guid.NewGuid());

        await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        var movement = Assert.Single(f.CashMovements.Movements);
        Assert.Equal(CashMovementType.CashSaleTender, movement.Type);
        Assert.Equal(12.10m, movement.Amount);
        Assert.Equal(f.RegisterSessions.Session!.Id, movement.RegisterSessionId);
    }

    [Fact]
    public async Task CompleteSaleAsync_CardOnlyTender_DoesNotRecordCashMovement()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null, new[] { new TenderLine(TenderType.Card, 12.10m) }, Guid.NewGuid());

        var result = await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        Assert.Equal(CompleteSaleStatus.Success, result.Status);
        Assert.Empty(f.CashMovements.Movements);
    }

    [Fact]
    public async Task CompleteSaleAsync_MixedCashCardTenderType_ReturnsRejected()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null, new[] { new TenderLine(TenderType.MixedCashCard, 12.10m) }, Guid.NewGuid());

        var result = await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        Assert.Equal(CompleteSaleStatus.Rejected, result.Status);
        Assert.Equal(RejectionCode.PaymentValidationFailed, result.RejectionCode);
        Assert.Empty(f.CashMovements.Movements);
    }

    [Fact]
    public async Task CompleteSaleAsync_SplitCashAndCardTender_RecordsOnlyCashPortion()
    {
        var f = SetUpHappyPath();
        var svc = f.BuildService();
        await svc.AddLineAsync(f.Carts.Cart!.Id, new AddLineRequest("8412345678901", 1));
        var request = new CompleteSaleRequest(
            f.Registers.Register!.Id, null,
            new[] { new TenderLine(TenderType.Cash, 5.00m), new TenderLine(TenderType.Card, 7.10m) },
            Guid.NewGuid());

        var result = await svc.CompleteSaleAsync(f.Carts.Cart.Id, request);

        Assert.Equal(CompleteSaleStatus.Success, result.Status);
        var movement = Assert.Single(f.CashMovements.Movements);
        Assert.Equal(5.00m, movement.Amount); // only the cash portion, not the full 12.10 total
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
