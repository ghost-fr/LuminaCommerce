namespace Lumina.Application.Ports;

/// <summary>
/// Minimal transaction-control abstraction. Used by PosSaleService to wrap
/// the stock-availability-check-through-stock-decrement critical section in a
/// real database transaction — without this, a stock check (read) and the
/// eventual stock movement (write) happen as separate, unsynchronized
/// operations, and two concurrent sales of the last unit of a product could
/// both pass the availability check before either decrements. See
/// Infrastructure's UnitOfWork implementation and GAPFIXES_NOTES.md for the
/// empirically-verified mechanism this relies on.
///
/// Deliberately minimal (Begin/Commit/Rollback only) rather than a general
/// repository-spanning Unit-of-Work pattern — this codebase's repositories
/// already share one DbContext per request via DI scoping, which already
/// gives them shared-transaction visibility once one is open; this interface
/// only adds the ability to explicitly control WHEN that transaction starts
/// and ends around a specific critical section.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Begins a transaction that acquires SQLite's write-intent lock
    /// immediately at BEGIN (not deferred until the first write) — this is
    /// what makes a check-then-write critical section safe under concurrency.
    /// A concurrent caller's BeginAsync blocks until this transaction commits
    /// or rolls back, even if that concurrent caller only performs reads
    /// before reaching its own write.</summary>
    Task BeginAsync(CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);

    Task RollbackAsync(CancellationToken ct = default);
}
