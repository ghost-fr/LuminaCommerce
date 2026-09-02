using Lumina.Application.Ports;
using Lumina.Domain.Cash;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

public class RegisterSessionRepository : IRegisterSessionRepository
{
    private readonly LuminaDbContext _db;
    public RegisterSessionRepository(LuminaDbContext db) => _db = db;

    public Task<RegisterSession?> FindByIdAsync(Guid registerSessionId, CancellationToken ct = default) =>
        _db.RegisterSessions.FirstOrDefaultAsync(s => s.Id == registerSessionId, ct);

    public Task<RegisterSession?> FindOpenByRegisterIdAsync(Guid registerId, CancellationToken ct = default) =>
        _db.RegisterSessions.FirstOrDefaultAsync(
            s => s.RegisterId == registerId && s.Status == RegisterSessionStatus.Open, ct);

    public async Task AddAsync(RegisterSession session, CancellationToken ct = default) =>
        await _db.RegisterSessions.AddAsync(session, ct);

    public Task UpdateAsync(RegisterSession session, CancellationToken ct = default)
    {
        _db.RegisterSessions.Update(session);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class CashMovementRepository : ICashMovementRepository
{
    private readonly LuminaDbContext _db;
    public CashMovementRepository(LuminaDbContext db) => _db = db;

    public async Task AddAsync(CashMovement movement, CancellationToken ct = default) =>
        await _db.CashMovements.AddAsync(movement, ct);

    public async Task<decimal> GetTotalForSessionAsync(Guid registerSessionId, CancellationToken ct = default) =>
        await _db.CashMovements
            .Where(m => m.RegisterSessionId == registerSessionId)
            .SumAsync(m => (decimal?)m.Amount, ct) ?? 0m;

    public async Task<IReadOnlyList<CashMovement>> GetForSessionAsync(Guid registerSessionId, CancellationToken ct = default) =>
        await _db.CashMovements
            .Where(m => m.RegisterSessionId == registerSessionId)
            .ToListAsync(ct);
}
