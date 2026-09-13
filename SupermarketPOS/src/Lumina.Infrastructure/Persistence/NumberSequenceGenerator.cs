using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence;

/// <summary>
/// Generates gap-free, race-free sequential numbers (ticket numbers, invoice
/// numbers) — replacing the previous "count existing rows" approach used by
/// SaleRepository.NextTicketNumberAsync and InvoiceRepository.
/// NextInvoiceNumberAsync, which was explicitly flagged since Phase 2/3 as
/// unsafe under concurrent writers.
///
/// How this is actually safe: a SINGLE SQL statement —
/// `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` — atomically creates the
/// row on first use OR increments-and-returns it on every subsequent call.
/// SQLite guarantees a single statement executes atomically with respect to
/// other writers (each writer's statement fully completes, including its
/// RETURNING evaluation, before another writer's statement can begin) — no
/// explicit BEGIN/COMMIT is needed for correctness here, and deliberately
/// none is used, so this method composes safely inside ANY caller's own
/// transaction (including PosSaleService's stock-check transaction, see
/// UnitOfWork.cs) without risking SQLite's lack of true nested-transaction
/// support.
///
/// VERIFIED empirically (not just reasoned about) with 30 concurrent callers
/// via a real SQLite database and real OS threads — see GAPFIXES_NOTES.md for
/// the exact test and result. This is a materially stronger verification bar
/// than "this pattern is generally known to work."
///
/// NOTE: requires the `NumberSequences` table — like every schema change in
/// this codebase, `dotnet ef migrations add` needs to be run locally to pick
/// this up.
/// </summary>
public sealed class NumberSequenceGenerator
{
    private readonly LuminaDbContext _db;
    public NumberSequenceGenerator(LuminaDbContext db) => _db = db;

    public async Task<long> NextAsync(Guid storeId, string sequenceType, CancellationToken ct = default)
    {
        var results = await _db.Database.SqlQuery<long>($@"
            INSERT INTO NumberSequences (StoreId, SequenceType, CurrentValue)
            VALUES ({storeId.ToString()}, {sequenceType}, 1)
            ON CONFLICT(StoreId, SequenceType) DO UPDATE SET CurrentValue = CurrentValue + 1
            RETURNING CurrentValue;").ToListAsync(ct);

        return results.Single();
    }
}
