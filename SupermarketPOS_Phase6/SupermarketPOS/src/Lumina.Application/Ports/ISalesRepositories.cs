using Lumina.Domain.Sales;

namespace Lumina.Application.Ports;

public interface ICartRepository
{
    Task<Cart?> FindByIdAsync(Guid cartId, CancellationToken ct = default);
    Task AddAsync(Cart cart, CancellationToken ct = default);

    /// <summary>Must be called after mutating a Cart returned by FindByIdAsync/AddAsync
    /// and before SaveChangesAsync — Cart is a plain domain object, not an EF-tracked
    /// entity (see Infrastructure's CartRepository for why: it persists via a JSON
    /// projection to keep EF out of the aggregate's internals). Forgetting this call
    /// after AddLine/RemoveQuantity means the mutation is silently lost on save.</summary>
    Task UpdateAsync(Cart cart, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IRegisterRepository
{
    Task<Register?> FindByIdAsync(Guid registerId, CancellationToken ct = default);

    /// <summary>NEW as of Phase 6 — closes a gap found while building register
    /// open/close: nothing before this called Register.Open()/Close() from
    /// Application code, so nothing needed a way to persist that mutation.
    /// RegisterSessionService is the first real caller.</summary>
    Task UpdateAsync(Register register, CancellationToken ct = default);
}

public interface ISaleRepository
{
    Task AddAsync(Sale sale, CancellationToken ct = default);

    /// <summary>Used for idempotency checks — find a prior sale created from the
    /// same IdempotencyKey, so a retried CompleteSaleAsync call can return the
    /// original result instead of creating a duplicate sale.</summary>
    Task<Sale?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken ct = default);

    /// <summary>Generates the next ticket number for a store, e.g. "T-000123".
    /// Must be gap-free and monotonic within the store per fiscal requirements —
    /// implementation detail lives in Infrastructure, this is just the port.</summary>
    Task<string> NextTicketNumberAsync(Guid storeId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// Phase 4: backed by the real append-only stock ledger (Lumina.Application.Stock.
/// LedgerBackedStockAvailabilityChecker). Signature takes StoreId explicitly since
/// stock is tracked per-store, not globally.
/// </summary>
public interface IStockAvailabilityChecker
{
    Task<bool> IsAvailableAsync(Guid storeId, Guid productId, decimal requestedQuantity, CancellationToken ct = default);
}
