namespace Lumina.Domain.Cash;

public enum CashMovementType
{
    /// <summary>The cash portion of a completed sale's tenders. Card/mixed
    /// tenders' non-cash portion never generates a CashMovement — only physical
    /// cash entering the drawer does.</summary>
    CashSaleTender,

    /// <summary>Cash paid back to a customer on a refund (no refund flow exists
    /// yet — this value exists so the type is ready when one is built, same
    /// forward-compatibility reasoning as Invoice.SourceQuoteId in Phase 5).</summary>
    CashRefundTender,

    /// <summary>Manual cash added to the drawer mid-session (e.g. a top-up).
    /// Always positive, always requires a Reason.</summary>
    CashIn,

    /// <summary>Manual cash removed from the drawer mid-session (e.g. a cash
    /// drop to a safe). Always negative, always requires a Reason.</summary>
    CashOut
}

/// <summary>
/// A single append-only cash-drawer ledger entry, scoped to one RegisterSession.
/// Same discipline as Domain.Stock.StockMovement: every change is a new row,
/// nothing is ever edited or deleted. The opening float itself is NOT a
/// CashMovement — it lives as a field on RegisterSession — so
/// "sum of this session's movements" and "opening float" are always two
/// unambiguous, separately-named things, never confused with each other.
/// </summary>
public class CashMovement
{
    public Guid Id { get; private set; }
    public Guid RegisterSessionId { get; private set; }
    public CashMovementType Type { get; private set; }

    /// <summary>Positive = cash added to the drawer, negative = cash removed.
    /// Never zero.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Required for CashIn/CashOut (the operator must state why they're
    /// touching the drawer manually); null for CashSaleTender (the ReferenceId —
    /// the SaleId — is the "reason" in that case).</summary>
    public string? Reason { get; private set; }

    /// <summary>SaleId for CashSaleTender/CashRefundTender; null for manual
    /// CashIn/CashOut.</summary>
    public Guid? ReferenceId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private CashMovement() { }

    public CashMovement(Guid id, Guid registerSessionId, CashMovementType type, decimal amount, string? reason, Guid? referenceId)
    {
        if (amount == 0m)
            throw new ArgumentException("A cash movement must have a nonzero amount.", nameof(amount));

        if (type is CashMovementType.CashIn or CashMovementType.CashOut && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException($"{type} movements require a reason.", nameof(reason));

        if (type == CashMovementType.CashOut && amount > 0m)
            throw new ArgumentException("CashOut amount must be negative.", nameof(amount));
        if (type == CashMovementType.CashIn && amount < 0m)
            throw new ArgumentException("CashIn amount must be positive.", nameof(amount));
        if (type == CashMovementType.CashSaleTender && amount < 0m)
            throw new ArgumentException("CashSaleTender amount must be positive — cash coming INTO the drawer from a sale.", nameof(amount));
        if (type == CashMovementType.CashRefundTender && amount > 0m)
            throw new ArgumentException("CashRefundTender amount must be negative — cash going OUT of the drawer to the customer.", nameof(amount));

        Id = id;
        RegisterSessionId = registerSessionId;
        Type = type;
        Amount = amount;
        Reason = reason;
        ReferenceId = referenceId;
        OccurredAt = DateTimeOffset.UtcNow;
    }
}
