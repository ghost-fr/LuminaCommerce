namespace Lumina.Domain.Cash;

public enum RegisterSessionStatus
{
    Open,
    Closed
}

/// <summary>
/// One open-to-close cash-handling cycle for a Domain.Sales.Register. A Register
/// (the physical/logical terminal) can be opened and closed many times — once
/// per shift/day typically — and each cycle is its own RegisterSession, with its
/// own opening float, its own cash movements, and its own reconciliation at
/// close. This is deliberately a separate aggregate from Register, not a field
/// added to it: Register is the terminal itself (long-lived), RegisterSession is
/// one day's cash accountability record (short-lived, one per shift).
///
/// ExpectedClosingCash and Discrepancy are computed OUTSIDE this type (by
/// RegisterSessionService, which has access to the CashMovement repository) and
/// passed into Close() — this aggregate never reaches into a repository itself,
/// same "no I/O in Domain" discipline as Sale requiring pre-validated tender
/// totals before construction.
/// </summary>
public class RegisterSession
{
    public Guid Id { get; private set; }
    public Guid RegisterId { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid OpenedByUserId { get; private set; }
    public decimal OpeningFloat { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public RegisterSessionStatus Status { get; private set; }

    public Guid? ClosedByUserId { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public decimal? DeclaredClosingCash { get; private set; }
    public decimal? ExpectedClosingCash { get; private set; }
    public decimal? Discrepancy { get; private set; }

    private RegisterSession() { }

    public RegisterSession(Guid id, Guid registerId, Guid storeId, Guid openedByUserId, decimal openingFloat, DateTimeOffset? openedAt = null)
    {
        if (openingFloat < 0m)
            throw new ArgumentOutOfRangeException(nameof(openingFloat), "Opening float cannot be negative.");

        Id = id;
        RegisterId = registerId;
        StoreId = storeId;
        OpenedByUserId = openedByUserId;
        OpeningFloat = openingFloat;
        OpenedAt = openedAt ?? DateTimeOffset.UtcNow;
        Status = RegisterSessionStatus.Open;
    }

    /// <summary>expectedClosingCash is OpeningFloat + sum of this session's
    /// CashMovements — computed by the caller (RegisterSessionService), not by
    /// this method. Discrepancy = declaredClosingCash - expectedClosingCash;
    /// positive means more cash was counted than expected, negative means
    /// less.</summary>
    public void Close(Guid closedByUserId, decimal declaredClosingCash, decimal expectedClosingCash, DateTimeOffset? closedAt = null)
    {
        if (Status != RegisterSessionStatus.Open)
            throw new InvalidOperationException("This register session is already closed.");
        if (declaredClosingCash < 0m)
            throw new ArgumentOutOfRangeException(nameof(declaredClosingCash), "Declared closing cash cannot be negative.");

        ClosedByUserId = closedByUserId;
        DeclaredClosingCash = declaredClosingCash;
        ExpectedClosingCash = expectedClosingCash;
        Discrepancy = declaredClosingCash - expectedClosingCash;
        ClosedAt = closedAt ?? DateTimeOffset.UtcNow;
        Status = RegisterSessionStatus.Closed;
    }
}
