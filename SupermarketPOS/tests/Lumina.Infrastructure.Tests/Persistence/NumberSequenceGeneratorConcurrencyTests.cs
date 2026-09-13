using Lumina.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lumina.Infrastructure.Tests.Persistence;

/// <summary>
/// Real SQLite integration tests (in-memory, shared-cache mode — multiple
/// separate connections pointed at the same in-memory database, simulating
/// genuinely concurrent callers the way FakeSaleRepository-style unit tests
/// cannot: this exercises the ACTUAL transaction + RETURNING SQL, not a
/// mocked stand-in for it). This is the direct proof that
/// NumberSequenceGenerator eliminates the ticket/invoice numbering race
/// condition flagged since Phase 2/3 — not just that the code compiles.
/// </summary>
public class NumberSequenceGeneratorConcurrencyTests
{
    private static async Task<(SqliteConnection KeepAlive, DbContextOptions<LuminaDbContext> Options)> SetUpSharedDbAsync(string dbName)
    {
        // A shared-cache in-memory SQLite DB is visible to multiple separate
        // connections as long as at least one stays open for its lifetime —
        // this connection exists only to keep the in-memory DB alive; it is
        // never used directly for queries.
        var keepAlive = new SqliteConnection($"Data Source=file:{dbName}?mode=memory&cache=shared");
        await keepAlive.OpenAsync();

        var options = new DbContextOptionsBuilder<LuminaDbContext>()
            .UseSqlite($"Data Source=file:{dbName}?mode=memory&cache=shared")
            .Options;

        await using var setupDb = new LuminaDbContext(options);
        await setupDb.Database.EnsureCreatedAsync(); // builds schema from the model directly — no migration needed for this test

        return (keepAlive, options);
    }

    [Fact]
    public async Task NextAsync_20ConcurrentCallsSameStore_ProducesDistinctSequentialValues_NoDuplicatesNoGaps()
    {
        var (keepAlive, options) = await SetUpSharedDbAsync(nameof(NextAsync_20ConcurrentCallsSameStore_ProducesDistinctSequentialValues_NoDuplicatesNoGaps));
        try
        {
            var storeId = Guid.NewGuid();
            const int concurrentCalls = 20;

            // Each task gets its OWN DbContext/connection — genuinely separate
            // callers, not the same context used from multiple threads (which
            // would be a different, invalid test — EF Core DbContext is not
            // thread-safe for concurrent use on one instance).
            var tasks = Enumerable.Range(0, concurrentCalls).Select(async _ =>
            {
                await using var db = new LuminaDbContext(options);
                var generator = new NumberSequenceGenerator(db);
                return await generator.NextAsync(storeId, "Ticket");
            });

            var results = await Task.WhenAll(tasks);

            Assert.Equal(concurrentCalls, results.Distinct().Count()); // no two callers got the same number
            var expected = Enumerable.Range(1, concurrentCalls).Select(i => (long)i).OrderBy(x => x);
            Assert.Equal(expected, results.OrderBy(x => x)); // exactly 1..20, no gaps
        }
        finally
        {
            await keepAlive.CloseAsync();
        }
    }

    [Fact]
    public async Task NextAsync_DifferentStores_AreIndependentSequences()
    {
        var (keepAlive, options) = await SetUpSharedDbAsync(nameof(NextAsync_DifferentStores_AreIndependentSequences));
        try
        {
            var storeA = Guid.NewGuid();
            var storeB = Guid.NewGuid();

            await using var dbA = new LuminaDbContext(options);
            var firstA = await new NumberSequenceGenerator(dbA).NextAsync(storeA, "Ticket");

            await using var dbB = new LuminaDbContext(options);
            var firstB = await new NumberSequenceGenerator(dbB).NextAsync(storeB, "Ticket");

            Assert.Equal(1, firstA);
            Assert.Equal(1, firstB); // each store's ticket sequence starts independently at 1
        }
        finally
        {
            await keepAlive.CloseAsync();
        }
    }

    [Fact]
    public async Task NextAsync_DifferentSequenceTypesSameStore_AreIndependentSequences()
    {
        var (keepAlive, options) = await SetUpSharedDbAsync(nameof(NextAsync_DifferentSequenceTypesSameStore_AreIndependentSequences));
        try
        {
            var storeId = Guid.NewGuid();
            await using var db = new LuminaDbContext(options);
            var generator = new NumberSequenceGenerator(db);

            var ticket1 = await generator.NextAsync(storeId, "Ticket");
            var invoice1 = await generator.NextAsync(storeId, "Invoice");
            var ticket2 = await generator.NextAsync(storeId, "Ticket");

            Assert.Equal(1, ticket1);
            Assert.Equal(1, invoice1); // Invoice sequence independent from Ticket
            Assert.Equal(2, ticket2);
        }
        finally
        {
            await keepAlive.CloseAsync();
        }
    }
}
