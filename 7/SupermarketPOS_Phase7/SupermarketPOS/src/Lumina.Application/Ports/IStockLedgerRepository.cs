using Lumina.Domain.Stock;

namespace Lumina.Application.Ports;

public interface IStockLedgerRepository
{
    Task<decimal> GetQuantityOnHandAsync(Guid storeId, Guid productId, CancellationToken ct = default);

    Task AddMovementAsync(StockMovement movement, CancellationToken ct = default);

    /// <summary>NEW as of Phase 7 — backs ReportingService's stock-on-hand
    /// report. Returns EVERY product that has at least one movement at this
    /// store, keyed by ProductId, value = current quantity on hand (which may
    /// legitimately be zero or negative — a negative on-hand means more was
    /// sold than was ever recorded as received, worth surfacing to the
    /// operator as a data-quality signal, not hidden). Products with zero
    /// movements ever are simply absent from the dictionary, not present with
    /// a 0 value — same "absent means never touched" vs "present with 0 means
    /// balanced out" distinction GetQuantityOnHandAsync already has implicitly.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandByProductAsync(Guid storeId, CancellationToken ct = default);
}
