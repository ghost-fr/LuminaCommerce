using Lumina.Infrastructure.Persistence;
using Lumina.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lumina.Infrastructure.Tests.Persistence;

public sealed class SaleRepositoryTicketNumberTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public SaleRepositoryTicketNumberTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"lumina-ticket-test-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task NextTicketNumberAsync_ConcurrentCallsSameStore_NeverCollideAndAreContiguous()
    {
        var storeId = Guid.NewGuid();
        const int concurrentCalls = 25;

        var tasks = Enumerable.Range(0, concurrentCalls).Select(async _ =>
        {
            using var db = CreateContext();
            var repository = new SaleRepository(db);
            return await repository.NextTicketNumberAsync(storeId);
        });

        var ticketNumbers = await Task.WhenAll(tasks);

        var distinct = ticketNumbers.Distinct().ToArray();
        Assert.True(
            distinct.Length == concurrentCalls,
            $"Expected {concurrentCalls} distinct ticket numbers but got {distinct.Length}: " +
            $"[{string.Join(", ", ticketNumbers.OrderBy(n => n))}]");

        var expected = Enumerable.Range(1, concurrentCalls).Select(n => $"T-{n:D6}").ToHashSet();
        Assert.Equal(expected, ticketNumbers.ToHashSet());
    }

    [Fact]
    public async Task NextTicketNumberAsync_DifferentStores_SequencesAreIndependent()
    {
        var storeA = Guid.NewGuid();
        var storeB = Guid.NewGuid();

        using var db = CreateContext();
        var repository = new SaleRepository(db);

        var firstForA = await repository.NextTicketNumberAsync(storeA);
        var firstForB = await repository.NextTicketNumberAsync(storeB);
        var secondForA = await repository.NextTicketNumberAsync(storeA);

        Assert.Equal("T-000001", firstForA);
        Assert.Equal("T-000001", firstForB);
        Assert.Equal("T-000002", secondForA);
    }

    private LuminaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LuminaDbContext>()
            .UseSqlite(_connectionString)
            .Options;
        return new LuminaDbContext(options);
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch (IOException) { }
    }
}
