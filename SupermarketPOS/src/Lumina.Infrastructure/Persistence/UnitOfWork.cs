namespace Lumina.Infrastructure.Persistence;

/// <summary>
/// Concrete IUnitOfWork. Uses raw SQL "BEGIN IMMEDIATE" rather than EF Core's
/// Database.BeginTransactionAsync() because EF Core's transaction API doesn't
/// expose SQLite's IMMEDIATE mode — the default ("DEFERRED") does NOT acquire
/// a write lock until the first write statement, which would NOT protect a
/// check-then-write critical section (a concurrent reader could still read
/// stale data between the check and the write). IMMEDIATE acquires the lock
/// right at BEGIN, which does.
///
/// VERIFICATION STATUS — read before trusting this in production:
/// - The core SQLite locking claim (a second connection's BEGIN IMMEDIATE
///   blocks until the first commits, and its subsequent read then sees the
///   first transaction's already-committed write, not stale data) was
///   VERIFIED EMPIRICALLY using real SQLite + real OS threads outside of
///   .NET/EF Core (a Python reproduction — see GAPFIXES_NOTES.md for the
///   exact test and output). High confidence.
/// - The EF-Core-specific mechanics here (calling OpenConnectionAsync() to
///   pin the connection BEFORE issuing raw transaction-control SQL, so that
///   EF Core's own connection lifecycle doesn't open a different underlying
///   connection for a later query within the same "transaction") follow
///   Microsoft's documented pattern for manual/raw transaction control in EF
///   Core. This specific code has NOT been run against a real EF Core
///   assembly by me — I don't have the .NET SDK available in this
///   environment. Materially lower confidence than the SQLite behavior
///   itself. Run `dotnet test` locally (see the new
///   NumberSequenceGeneratorConcurrencyTests as a model — a similar
///   concurrency test for THIS class, using PosSaleService and two real
///   concurrent CompleteSaleAsync calls for the same last-unit-of-stock
///   scenario, would be the right next verification step and is flagged as
///   not yet written).
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly LuminaDbContext _db;
    private bool _inTransaction;

    public UnitOfWork(LuminaDbContext db) => _db = db;

    public async Task BeginAsync(CancellationToken ct = default)
    {
        if (_inTransaction)
            throw new InvalidOperationException("A transaction is already in progress on this unit of work.");

        // Pin the connection open before issuing raw transaction-control SQL —
        // see class remarks above.
        await _db.Database.OpenConnectionAsync(ct);
        await _db.Database.ExecuteSqlRawAsync("BEGIN IMMEDIATE", ct);
        _inTransaction = true;
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (!_inTransaction) return;
        await _db.Database.ExecuteSqlRawAsync("COMMIT", ct);
        _inTransaction = false;
        await _db.Database.CloseConnectionAsync();
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (!_inTransaction) return;
        try
        {
            await _db.Database.ExecuteSqlRawAsync("ROLLBACK", ct);
        }
        finally
        {
            _inTransaction = false;
            await _db.Database.CloseConnectionAsync();
        }
    }
}
