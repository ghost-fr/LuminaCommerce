using Lumina.Domain.Cash;

namespace Lumina.Application.Ports;

public interface IRegisterSessionRepository
{
    Task<RegisterSession?> FindByIdAsync(Guid registerSessionId, CancellationToken ct = default);
    Task<RegisterSession?> FindOpenByRegisterIdAsync(Guid registerId, CancellationToken ct = default);
    Task AddAsync(RegisterSession session, CancellationToken ct = default);
    Task UpdateAsync(RegisterSession session, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ICashMovementRepository
{
    Task AddAsync(CashMovement movement, CancellationToken ct = default);

    Task<decimal> GetTotalForSessionAsync(Guid registerSessionId, CancellationToken ct = default);

    /// <summary>NEW as of Phase 7 — backs ReportingService's register session
    /// report, which needs the breakdown BY TYPE (CashSalesTotal, CashInTotal,
    /// CashOutTotal separately), not just the net total GetTotalForSessionAsync
    /// provides. ReportingService groups this raw list by Type itself rather
    /// than the repository exposing several narrow sum-by-type methods.</summary>
    Task<IReadOnlyList<CashMovement>> GetForSessionAsync(Guid registerSessionId, CancellationToken ct = default);
}
