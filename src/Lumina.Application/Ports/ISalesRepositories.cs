using Lumina.Domain.Sales;

namespace Lumina.Application.Ports;

public interface ICartRepository
{
    Task<Cart?> FindByIdAsync(Guid cartId, CancellationToken ct = default);
    Task AddAsync(Cart cart, CancellationToken ct = default);

    /// <summary>Must be called after mutating a Cart returned by FindByIdAsync/AddAsync
    /// and before SaveChangesAsync.</summary>
    Task UpdateAsync(Cart cart, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IRegisterRepository
{
    Task<Register?> FindByIdAsync(Guid registerId, CancellationToken ct = default);

    /// <summary>First open register for the store, or null.</summary>
    Task<Register?> FindOpenByStoreIdAsync(Guid storeId, CancellationToken ct = default);
}

public interface ISaleRepository
{
    Task AddAsync(Sale sale, CancellationToken ct = default);

    Task<Sale?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken ct = default);

    Task<string> NextTicketNumberAsync(Guid storeId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IStockAvailabilityChecker
{
    Task<bool> IsAvailableAsync(Guid productId, decimal requestedQuantity, CancellationToken ct = default);
}
