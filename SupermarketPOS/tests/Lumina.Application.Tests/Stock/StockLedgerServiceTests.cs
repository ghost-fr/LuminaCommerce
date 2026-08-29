using Lumina.Application.Ports;
using Lumina.Application.Stock;
using Lumina.Domain.Stock;
using Xunit;

namespace Lumina.Application.Tests.Stock;

public class StockLedgerServiceTests
{
    private sealed class FakeLedgerRepository : IStockLedgerRepository
    {
        public readonly List<StockMovement> Movements = new();
        public Task<decimal> GetQuantityOnHandAsync(Guid storeId, Guid productId, CancellationToken ct = default) =>
            Task.FromResult(Movements.Where(m => m.StoreId == storeId && m.ProductId == productId).Sum(m => m.QuantityDelta));
        public Task AddMovementAsync(StockMovement movement, CancellationToken ct = default)
        {
            Movements.Add(movement);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GetQuantityOnHandAsync_NoMovements_ReturnsZero()
    {
        var svc = new StockLedgerService(new FakeLedgerRepository());
        var onHand = await svc.GetQuantityOnHandAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(0m, onHand);
    }

    [Fact]
    public async Task RecordSaleMovementsAsync_OneMovementPerLine_NegativeQuantity()
    {
        var repo = new FakeLedgerRepository();
        var svc = new StockLedgerService(repo);
        var storeId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        await svc.RecordSaleMovementsAsync(storeId, saleId, new[] { (productA, 2m), (productB, 1m) });

        Assert.Equal(2, repo.Movements.Count);
        Assert.All(repo.Movements, m => Assert.Equal(StockMovementReason.Sale, m.Reason));
        Assert.All(repo.Movements, m => Assert.Equal(saleId, m.ReferenceId));
        Assert.Equal(-2m, repo.Movements.First(m => m.ProductId == productA).QuantityDelta);
        Assert.Equal(-1m, repo.Movements.First(m => m.ProductId == productB).QuantityDelta);
    }

    [Fact]
    public async Task AdjustAsync_PositiveDelta_UsesIncreaseReason()
    {
        var repo = new FakeLedgerRepository();
        var svc = new StockLedgerService(repo);

        await svc.AdjustAsync(Guid.NewGuid(), Guid.NewGuid(), 5m);

        Assert.Equal(StockMovementReason.ManualAdjustmentIncrease, repo.Movements.Single().Reason);
    }

    [Fact]
    public async Task AdjustAsync_NegativeDelta_UsesDecreaseReason()
    {
        var repo = new FakeLedgerRepository();
        var svc = new StockLedgerService(repo);

        await svc.AdjustAsync(Guid.NewGuid(), Guid.NewGuid(), -3m);

        Assert.Equal(StockMovementReason.ManualAdjustmentDecrease, repo.Movements.Single().Reason);
    }

    [Fact]
    public async Task AdjustAsync_ZeroDelta_Throws()
    {
        var svc = new StockLedgerService(new FakeLedgerRepository());
        await Assert.ThrowsAsync<ArgumentException>(() => svc.AdjustAsync(Guid.NewGuid(), Guid.NewGuid(), 0m));
    }

    [Fact]
    public async Task GetQuantityOnHandAsync_SumsAllMovementsForStoreAndProduct()
    {
        var repo = new FakeLedgerRepository();
        var svc = new StockLedgerService(repo);
        var storeId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await svc.AdjustAsync(storeId, productId, 10m);   // receiving-style increase
        await svc.RecordSaleMovementsAsync(storeId, Guid.NewGuid(), new[] { (productId, 3m) });

        var onHand = await svc.GetQuantityOnHandAsync(storeId, productId);
        Assert.Equal(7m, onHand);
    }
}
