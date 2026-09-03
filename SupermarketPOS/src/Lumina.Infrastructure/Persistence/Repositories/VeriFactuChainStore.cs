using Lumina.Application.Ports;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IVeriFactuChainStore. Consumed by Lumina.Fiscal's
/// FiscalRecordGenerator.
///
/// UPDATED as part of the correctness-hardening pass: AppendAsync now stores
/// PreviousHash and GeneratedAt (previously only RecordHash/CreatedAt).
/// GetRecentLinksAsync is new — backs the chain-integrity pre-check.
/// </summary>
public class VeriFactuChainStore : IVeriFactuChainStore
{
    private readonly LuminaDbContext _db;
    public VeriFactuChainStore(LuminaDbContext db) => _db = db;

    public async Task<string> GetLastHashAsync(Guid sifBoundaryId, CancellationToken ct = default)
    {
        var last = await _db.VeriFactuChainRecords
            .Where(r => r.SifBoundaryId == sifBoundaryId)
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefaultAsync(ct);

        return last?.RecordHash ?? "";
    }

    public async Task<IReadOnlyList<ChainLink>> GetRecentLinksAsync(Guid sifBoundaryId, int count, CancellationToken ct = default)
    {
        var records = await _db.VeriFactuChainRecords
            .Where(r => r.SifBoundaryId == sifBoundaryId)
            .OrderByDescending(r => r.GeneratedAt)
            .Take(count)
            .ToListAsync(ct);

        return records.Select(r => new ChainLink(r.RecordHash, r.PreviousHash, r.GeneratedAt)).ToList();
    }

    public async Task AppendAsync(
        Guid sifBoundaryId, Guid recordId, string recordHash, string previousHash, DateTimeOffset generatedAt,
        CancellationToken ct = default)
    {
        await _db.VeriFactuChainRecords.AddAsync(new VeriFactuChainRecord
        {
            SifBoundaryId = sifBoundaryId,
            RecordId = recordId,
            RecordHash = recordHash,
            PreviousHash = previousHash,
            GeneratedAt = generatedAt,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);
        // NOTE: does not call SaveChangesAsync itself — relies on the same unit-of-work
        // commit as the Sale/Invoice it belongs to. Same flagged risk as before if
        // Fiscal and Sales/Commercial ever end up on different DbContext instances.
    }
}
