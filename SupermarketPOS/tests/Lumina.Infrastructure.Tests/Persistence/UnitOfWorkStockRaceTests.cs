using Lumina.Domain.Stock;
using Lumina.Infrastructure.Persistence;
using Lumina.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lumina.Infrastructure.Tests.Persistence;

/// <summary>
/// This is the verification flagged as missing in UnitOfWork.cs's remarks —
/// a real concurrency test using actual EF Core + real SQLite, not the
/// Python reproduction used to verify the underlying SQLite locking claim.
/// Simulates the exact scenario the fix exists for: two "sales" of the same
/// product, only 1 unit in stock, racing to check-then-decrement. Without
/// UnitOfWork wrapping the check+write, both could observe 1 unit available
/// and both succeed (overselling). This test proves that with the fix,
/// exactly one succeeds.
/// </summary>
public class UnitOfWorkStockRaceTests
{
    private static async Task<(SqliteConnection KeepAlive, DbContextOptions<LuminaDbContext> Options)> SetUpSharedDbAsync(string dbName)
    {
        var keepAlive = new SqliteConnection($"Data Source=file:{dbName}?mode=memory&cache=shared");
        await keepAlive.OpenAsync();

        var options = new DbContextOptionsBuilder<LuminaDbContext>()
            .UseSqlite($"Data Source=file:{dbName}?mode=memory&cache=shared")
            .Options;

        await using var setupDb = new LuminaDbContext(options);
        await setupDb.Database.EnsureCreatedAsync();

        return (keepAlive, options);
    }

    /// <summary>Mirrors the check-then-write shape of PosSaleService.CompleteSaleAsync's
    /// stock section, using the real StockLedgerRepository and UnitOfWork —
    /// not a fake, since the whole point is to exercise real DB locking.</summary>
    private static async Task<bool> TrySellOneUnitAsync(DbContextOptions<LuminaDbContext> options, Guid storeId, Guid productId)
    {
        await using var db = new LuminaDbContext(options);
        var stockLedger = new StockLedgerRepository(db);
        var unitOfWork = new UnitOfWork(db);

        await unitOfWork.BeginAsync();
        try
        {
            var onHand = await stockLedger.GetQuantityOnHandAsync(storeId, productId);
            if (onHand < 1m)
            {
                await unitOfWork.RollbackAsync();
                return false; // correctly rejected — insufficient stock
            }

            // Simulate the gap between check and write where a race could
            // occur if the transaction weren't holding a write-intent lock.
            await Task.Delay(50);

            await stockLedger.AddMovementAsync(
                new StockMovement(Guid.NewGuid(), storeId, productId, -1m, StockMovementReason.Sale, Guid.NewGuid()));
            await db.SaveChangesAsync();
            await unitOfWork.CommitAsync();
            return true; // sold
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }

    [Fact]
    public async Task TwoConcurrentSales_OnlyOneUnitInStock_ExactlyOneSucceeds()
    {
        var (keepAlive, options) = await SetUpSharedDbAsync(nameof(TwoConcurrentSales_OnlyOneUnitInStock_ExactlyOneSucceeds));
        try
        {
            var storeId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            // Seed exactly 1 unit of stock.
            await using (var seedDb = new LuminaDbContext(options))
            {
                var seedRepo = new StockLedgerRepository(seedDb);
                await seedRepo.AddMovementAsync(
                    new StockMovement(Guid.NewGuid(), storeId, productId, 1m, StockMovementReason.InitialStock, null));
                await seedDb.SaveChangesAsync();
            }

            // Two genuinely concurrent "sale" attempts for the last unit.
            var task1 = TrySellOneUnitAsync(options, storeId, productId);
            var task2 = TrySellOneUnitAsync(options, storeId, productId);
            var results = await Task.WhenAll(task1, task2);

            var successCount = results.Count(r => r);
            Assert.Equal(1, successCount); // NOT 2 — this is the actual anti-overselling assertion

            await using var verifyDb = new LuminaDbContext(options);
            var finalOnHand = await new StockLedgerRepository(verifyDb).GetQuantityOnHandAsync(storeId, productId);
            Assert.Equal(0m, finalOnHand); // exactly one unit sold, none oversold
        }
        finally
        {
            await keepAlive.CloseAsync();
        }
    }

    [Fact]
    public async Task TenConcurrentSales_FiveUnitsInStock_ExactlyFiveSucceed()
    {
        var (keepAlive, options) = await SetUpSharedDbAsync(nameof(TenConcurrentSales_FiveUnitsInStock_ExactlyFiveSucceed));
        try
        {
            var storeId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            await using (var seedDb = new LuminaDbContext(options))
            {
                var seedRepo = new StockLedgerRepository(seedDb);
                await seedRepo.AddMovementAsync(
                    new StockMovement(Guid.NewGuid(), storeId, productId, 5m, StockMovementReason.InitialStock, null));
                await seedDb.SaveChangesAsync();
            }

            var tasks = Enumerable.Range(0, 10).Select(_ => TrySellOneUnitAsync(options, storeId, productId));
            var results = await Task.WhenAll(tasks);

            Assert.Equal(5, results.Count(r => r));
            Assert.Equal(5, results.Count(r => !r));

            await using var verifyDb = new LuminaDbContext(options);
            var finalOnHand = await new StockLedgerRepository(verifyDb).GetQuantityOnHandAsync(storeId, productId);
            Assert.Equal(0m, finalOnHand);
        }
        finally
        {
            await keepAlive.CloseAsync();
        }
    }
}
