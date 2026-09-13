using Lumina.Domain.Cash;

namespace Lumina.Application.Ports;

public interface IRegisterSessionRepository
{
    Task<RegisterSession?> FindByIdAsync(Guid registerSessionId, CancellationToken ct = default);

    /// <summary>Null if the Register is currently closed (no open session) — a
    /// normal, expected result, not an error. Callers treat null as "this
    /// register isn't open," not as a not-found condition.</summary>
    Task<RegisterSession?> FindOpenByRegisterIdAsync(Guid registerId, CancellationToken ct = default);

    Task AddAsync(RegisterSession session, CancellationToken ct = default);

    /// <summary>Persists the Close() mutation. RegisterSession has no
    /// encapsulated collection (unlike Cart/Sale), but this codebase keeps the
    /// same "explicit Update call" discipline across every repository for
    /// consistency, rather than mixing EF-change-tracking-implicit-save in some
    /// repos and explicit-Update in others.</summary>
    Task UpdateAsync(RegisterSession session, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ICashMovementRepository
{
    /// <summary>Tracks the movement on the shared unit of work — does NOT call
    /// SaveChanges itself, same pattern as IStockLedgerRepository.AddMovementAsync.</summary>
    Task AddAsync(CashMovement movement, CancellationToken ct = default);

    /// <summary>SUM of Amount for all movements in this session. 0 if none yet.
    /// Does NOT include the session's OpeningFloat — that's a separate field on
    /// RegisterSession, deliberately not conflated with "movements."</summary>
    Task<decimal> GetTotalForSessionAsync(Guid registerSessionId, CancellationToken ct = default);
}
