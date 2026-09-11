using Lumina.Application.Ports;
using Lumina.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

public class VeriFactuChainStore : IVeriFactuChainStore
{
    private readonly LuminaDbContext _db;
    public VeriFactuChainStore(LuminaDbContext db) => _db = db;

    public async Task<VeriFactuChainLink?> GetLastLinkAsync(Guid sifBoundaryId, CancellationToken ct = default)
    {
        var last = await _db.VeriFactuChainRecords
            .Where(r => r.SifBoundaryId == sifBoundaryId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return last is null ? null : ToLink(last);
    }

    public async Task<VeriFactuChainLink?> GetSecondToLastLinkAsync(Guid sifBoundaryId, CancellationToken ct = default)
    {
        var secondToLast = await _db.VeriFactuChainRecords
            .Where(r => r.SifBoundaryId == sifBoundaryId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(1)
            .FirstOrDefaultAsync(ct);

        return secondToLast is null ? null : ToLink(secondToLast);
    }

    public async Task AppendAsync(
        Guid sifBoundaryId, Guid recordId, string recordHash, string previousRecordHash,
        DateTimeOffset recordGeneratedAt, CancellationToken ct = default)
    {
        await _db.VeriFactuChainRecords.AddAsync(new VeriFactuChainRecord
        {
            SifBoundaryId = sifBoundaryId,
            RecordId = recordId,
            RecordHash = recordHash,
            PreviousRecordHash = previousRecordHash,
            RecordGeneratedAt = recordGeneratedAt,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);
    }

    private static VeriFactuChainLink ToLink(VeriFactuChainRecord record) =>
        new(record.RecordHash, record.PreviousRecordHash, record.RecordGeneratedAt);
}
