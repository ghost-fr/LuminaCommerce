using Lumina.Application.Ports;
using Lumina.Domain.Cash;

namespace Lumina.Application.Cash;

/// <summary>
/// Called by PosSaleService after a sale successfully completes — records the
/// CASH portion only of the sale's tenders against the register's currently
/// open RegisterSession. Card/mixed tenders' non-cash portion never touches
/// this — only physical cash entering the drawer does. This is what makes
/// "register close reconciles to the ledger" mean something real: closing a
/// session sums exactly these movements, nothing else.
/// </summary>
public sealed class CashDrawerService
{
    private readonly IRegisterSessionRepository _sessions;
    private readonly ICashMovementRepository _movements;

    public CashDrawerService(IRegisterSessionRepository sessions, ICashMovementRepository movements)
    {
        _sessions = sessions;
        _movements = movements;
    }

    /// <summary>cashAmount is the sum of only the Cash-type TenderLines on the
    /// sale — PosSaleService computes that sum before calling this. A cashAmount
    /// of 0 (card-only sale) is a valid no-op, not an error: this method simply
    /// does nothing in that case, since a card sale never touches the physical
    /// drawer.</summary>
    public async Task RecordSaleCashTenderAsync(
        Guid registerId, Guid saleId, decimal cashAmount, CancellationToken ct = default)
    {
        if (cashAmount == 0m) return;

        var session = await _sessions.FindOpenByRegisterIdAsync(registerId, ct)
            ?? throw new InvalidOperationException(
                $"No open RegisterSession for register {registerId} while recording a cash tender for sale {saleId}. " +
                "This indicates Register.IsOpen and RegisterSession state have drifted out of sync — they should " +
                "only ever change together, via RegisterSessionService.OpenAsync/CloseAsync. This is a data " +
                "integrity bug, not a normal rejection path — it should never surface to the cashier as " +
                "'sale rejected.'");

        var movement = new CashMovement(
            Guid.NewGuid(), session.Id, CashMovementType.CashSaleTender, cashAmount, reason: null, referenceId: saleId);

        await _movements.AddAsync(movement, ct);
        // Deliberately no SaveChangesAsync here — this call happens INSIDE
        // PosSaleService.CompleteSaleAsync, before its own final
        // SaveChangesAsync, so the cash movement commits atomically with the
        // Sale itself. Same "record before the one final save" discipline as
        // StockLedgerService.RecordSaleMovementsAsync in Phase 4.
    }
}
