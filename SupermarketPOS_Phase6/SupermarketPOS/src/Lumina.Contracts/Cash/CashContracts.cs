namespace Lumina.Contracts.Cash;

public interface IRegisterSessionSummary
{
    Guid RegisterSessionId { get; }
    Guid RegisterId { get; }
    string Status { get; }
    decimal OpeningFloat { get; }
    DateTimeOffset OpenedAt { get; }
    decimal? ExpectedClosingCash { get; }
    decimal? DeclaredClosingCash { get; }
    decimal? Discrepancy { get; }
}

public record OpenRegisterRequest(Guid RegisterId, decimal OpeningFloat);

public enum OpenRegisterStatus
{
    Success,
    Rejected
}

public record OpenRegisterResult(OpenRegisterStatus Status, IRegisterSessionSummary? Session, string? RejectionReason);

public record CloseRegisterRequest(Guid RegisterId, decimal DeclaredClosingCash);

public enum CloseRegisterStatus
{
    Success,
    Rejected
}

/// <summary>Discrepancy = DeclaredClosingCash - ExpectedClosingCash. Positive
/// means more cash was counted than expected; negative means less. UI should
/// show this clearly and let the cashier/manager see it — never hide or
/// silently round it away, since it's the whole point of closing a register.</summary>
public record CloseRegisterResult(
    CloseRegisterStatus Status, decimal? ExpectedClosingCash, decimal? Discrepancy, string? RejectionReason);

public enum CashMovementDirection
{
    /// <summary>Cash added to the drawer (a top-up).</summary>
    In,
    /// <summary>Cash removed from the drawer (a drop to a safe).</summary>
    Out
}

public record RecordCashMovementRequest(Guid RegisterId, CashMovementDirection Direction, decimal Amount, string Reason);

public enum RecordCashMovementStatus
{
    Success,
    Rejected
}

public record RecordCashMovementResult(RecordCashMovementStatus Status, string? RejectionReason);

/// <summary>
/// UI-facing contract for register open/close and manual cash movements.
/// Concrete implementation is Lumina.Application.Cash.RegisterSessionService
/// (adapted — the Application service predates this Contracts interface and
/// uses its own request/result shapes internally; see PHASE6_NOTES.md for the
/// adapter note).
///
/// UI (Grok) may assume:
/// - A register must be Open (via OpenAsync) before CompleteSaleAsync
///   (Contracts.Pos) will accept sales against it — this was already true
///   before Phase 6 (RegisterNotOpen rejection code), but now opening/closing
///   actually does something beyond flipping a flag: it starts/stops real cash
///   tracking.
/// - RecordCashMovementAsync requires a non-empty Reason — same discipline as
///   IInvoiceService.CancelAsync (Contracts.Commercial).
/// - CloseRegisterResult always includes ExpectedClosingCash and Discrepancy on
///   success — UI should surface both, not just a pass/fail.
/// </summary>
public interface IRegisterSessionService
{
    Task<OpenRegisterResult> OpenAsync(OpenRegisterRequest request, CancellationToken ct = default);
    Task<CloseRegisterResult> CloseAsync(CloseRegisterRequest request, CancellationToken ct = default);
    Task<RecordCashMovementResult> RecordCashMovementAsync(RecordCashMovementRequest request, CancellationToken ct = default);
}
