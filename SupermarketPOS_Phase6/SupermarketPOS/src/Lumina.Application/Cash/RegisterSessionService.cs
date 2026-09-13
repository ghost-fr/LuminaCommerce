using Lumina.Application.Ports;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Cash;
using Lumina.Domain.Cash;

namespace Lumina.Application.Cash;

internal sealed class RegisterSessionSummaryView : IRegisterSessionSummary
{
    public Guid RegisterSessionId { get; init; }
    public Guid RegisterId { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal OpeningFloat { get; init; }
    public DateTimeOffset OpenedAt { get; init; }
    public decimal? ExpectedClosingCash { get; init; }
    public decimal? DeclaredClosingCash { get; init; }
    public decimal? Discrepancy { get; init; }
}

/// <summary>
/// Concrete implementation of Lumina.Contracts.Cash.IRegisterSessionService.
/// Orchestrates opening/closing a Register together with its RegisterSession —
/// these two must always change in lockstep (Register.IsOpen true iff an open
/// RegisterSession exists for it), and this service is the ONLY place that
/// relationship is maintained. Nothing else should call Register.Open()/Close()
/// directly.
///
/// The acting user (who opened/closed the register, who recorded a cash
/// movement) comes from IAuthService.CurrentSession — NOT passed in by the UI —
/// same reasoning as every other place in this codebase that needs "who is
/// doing this": the UI already knows via the logged-in session, so the
/// contract doesn't ask it to re-supply that.
/// </summary>
public sealed class RegisterSessionService : IRegisterSessionService
{
    private readonly IRegisterRepository _registers;
    private readonly IRegisterSessionRepository _sessions;
    private readonly ICashMovementRepository _movements;
    private readonly IAuthService _auth;

    public RegisterSessionService(
        IRegisterRepository registers, IRegisterSessionRepository sessions,
        ICashMovementRepository movements, IAuthService auth)
    {
        _registers = registers;
        _sessions = sessions;
        _movements = movements;
        _auth = auth;
    }

    public async Task<OpenRegisterResult> OpenAsync(OpenRegisterRequest request, CancellationToken ct = default)
    {
        if (request.OpeningFloat < 0m)
            return new OpenRegisterResult(OpenRegisterStatus.Rejected, null, "Opening float cannot be negative.");

        var userId = RequireCurrentUserId();

        var register = await _registers.FindByIdAsync(request.RegisterId, ct)
            ?? throw new InvalidOperationException($"Register {request.RegisterId} was not found.");

        if (register.IsOpen)
            return new OpenRegisterResult(OpenRegisterStatus.Rejected, null, "Register is already open.");

        var existingOpenSession = await _sessions.FindOpenByRegisterIdAsync(request.RegisterId, ct);
        if (existingOpenSession is not null)
            throw new InvalidOperationException(
                $"Data integrity issue: Register {request.RegisterId} reports closed but an open " +
                $"RegisterSession {existingOpenSession.Id} already exists for it.");

        register.Open();
        await _registers.UpdateAsync(register, ct);

        var session = new RegisterSession(Guid.NewGuid(), request.RegisterId, register.StoreId, userId, request.OpeningFloat);
        await _sessions.AddAsync(session, ct);
        await _sessions.SaveChangesAsync(ct); // commits Register.IsOpen + new RegisterSession together

        return new OpenRegisterResult(OpenRegisterStatus.Success, ToSummary(session), null);
    }

    public async Task<CloseRegisterResult> CloseAsync(CloseRegisterRequest request, CancellationToken ct = default)
    {
        var userId = RequireCurrentUserId();

        var register = await _registers.FindByIdAsync(request.RegisterId, ct)
            ?? throw new InvalidOperationException($"Register {request.RegisterId} was not found.");

        if (!register.IsOpen)
            return new CloseRegisterResult(CloseRegisterStatus.Rejected, null, null, "Register is not open.");

        var session = await _sessions.FindOpenByRegisterIdAsync(request.RegisterId, ct)
            ?? throw new InvalidOperationException(
                $"Data integrity issue: Register {request.RegisterId} reports open but no open RegisterSession exists for it.");

        if (request.DeclaredClosingCash < 0m)
            return new CloseRegisterResult(CloseRegisterStatus.Rejected, null, null, "Declared closing cash cannot be negative.");

        var movementsTotal = await _movements.GetTotalForSessionAsync(session.Id, ct);
        var expectedClosingCash = session.OpeningFloat + movementsTotal;

        session.Close(userId, request.DeclaredClosingCash, expectedClosingCash);
        await _sessions.UpdateAsync(session, ct);

        register.Close();
        await _registers.UpdateAsync(register, ct);

        await _sessions.SaveChangesAsync(ct); // commits both together, same atomicity discipline as OpenAsync

        return new CloseRegisterResult(CloseRegisterStatus.Success, expectedClosingCash, session.Discrepancy, null);
    }

    public async Task<RecordCashMovementResult> RecordCashMovementAsync(
        RecordCashMovementRequest request, CancellationToken ct = default)
    {
        RequireCurrentUserId(); // enforces "must be logged in," even though the id itself isn't stored on CashMovement today

        var session = await _sessions.FindOpenByRegisterIdAsync(request.RegisterId, ct);
        if (session is null)
            return new RecordCashMovementResult(RecordCashMovementStatus.Rejected, "Register is not open.");

        var type = request.Direction == CashMovementDirection.In ? CashMovementType.CashIn : CashMovementType.CashOut;
        var signedAmount = request.Direction == CashMovementDirection.In ? request.Amount : -request.Amount;

        CashMovement movement;
        try
        {
            movement = new CashMovement(Guid.NewGuid(), session.Id, type, signedAmount, request.Reason, referenceId: null);
        }
        catch (ArgumentException ex)
        {
            // Domain constructor validation surfaced as a normal rejection —
            // operator input error (amount/reason typed at the register), not a
            // programming error.
            return new RecordCashMovementResult(RecordCashMovementStatus.Rejected, ex.Message);
        }

        await _movements.AddAsync(movement, ct);
        await _sessions.SaveChangesAsync(ct); // shares the same DbContext/unit of work as sessions

        return new RecordCashMovementResult(RecordCashMovementStatus.Success, null);
    }

    private Guid RequireCurrentUserId() =>
        _auth.CurrentSession?.UserId
        ?? throw new InvalidOperationException("No user is currently logged in — cannot open/close a register or record a cash movement without an authenticated session.");

    private static IRegisterSessionSummary ToSummary(RegisterSession session) => new RegisterSessionSummaryView
    {
        RegisterSessionId = session.Id,
        RegisterId = session.RegisterId,
        Status = session.Status.ToString(),
        OpeningFloat = session.OpeningFloat,
        OpenedAt = session.OpenedAt,
        ExpectedClosingCash = session.ExpectedClosingCash,
        DeclaredClosingCash = session.DeclaredClosingCash,
        Discrepancy = session.Discrepancy
    };
}
