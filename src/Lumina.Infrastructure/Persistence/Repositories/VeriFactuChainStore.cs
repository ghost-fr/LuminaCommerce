using Lumina.Application.Ports;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IVeriFactuChainStore. Consumed by Lumina.Fiscal's
/// FiscalRecordGenerator (Fiscal references Application for the port, Infrastructure
/// implements it — same DI-resolved pattern as every other port in this codebase).
/// </summary>
public class VeriFactuChainStore : IVeriFactuChainStore
{
    private readonly LuminaDbContext _db;
    public VeriFactuChainStore(LuminaDbContext db) => _db = db;

    public async Task<string> GetLastHashAsync(Guid sifBoundaryId, CancellationToken ct = default)
    {
        var last = await _db.VeriFactuChainRecords
            .Where(r => r.SifBoundaryId == sifBoundaryId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return last?.RecordHash ?? "";
    }

    public async Task AppendAsync(Guid sifBoundaryId, Guid recordId, string recordHash, CancellationToken ct = default)
    {
        await _db.VeriFactuChainRecords.AddAsync(new VeriFactuChainRecord
        {
            SifBoundaryId = sifBoundaryId,
            RecordId = recordId,
            RecordHash = recordHash,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);
        // NOTE: does not call SaveChangesAsync itself — relies on the same unit-of-work
        // commit as the Sale it belongs to (PosSaleService calls _sales.SaveChangesAsync
        // after AttachVeriFactuRecord). If Fiscal and Sales end up on different
        // DbContext instances in a future refactor, this MUST become an explicit
        // transaction spanning both, or a chain link could persist without its Sale
        // (or vice versa) — flagged now while it's a one-line risk, not a redesign.
    }
}
