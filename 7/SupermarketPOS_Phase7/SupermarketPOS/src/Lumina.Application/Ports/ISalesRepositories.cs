using Lumina.Domain.Sales;

namespace Lumina.Application.Ports;

public interface ICartRepository
{
    Task<Cart?> FindByIdAsync(Guid cartId, CancellationToken ct = default);
    Task AddAsync(Cart cart, CancellationToken ct = default);
    Task UpdateAsync(Cart cart, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IRegisterRepository
{
    Task<Register?> FindByIdAsync(Guid registerId, CancellationToken ct = default);
    Task UpdateAsync(Register register, CancellationToken ct = default);
}

public interface ISaleRepository
{
    Task AddAsync(Sale sale, CancellationToken ct = default);

    Task<Sale?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken ct = default);

    Task<string> NextTicketNumberAsync(Guid storeId, CancellationToken ct = default);

    /// <summary>NEW as of Phase 7 — backs ReportingService's daily sales report
    /// and top-products query. Range is UTC start-inclusive/end-exclusive;
    /// callers (ReportingService) are responsible for converting the store's
    /// local calendar day into this UTC range BEFORE calling — this method does
    /// no timezone math itself, it's a plain range filter on the already-UTC
    /// Sale.CompletedAt column. Every Sale ever persisted is "completed" by
    /// definition (there's no partial/void Sale — see PHASE7_NOTES.md), so no
    /// additional status filtering is needed here.</summary>
    Task<IReadOnlyList<Sale>> FindByStoreAndDateRangeAsync(
        Guid storeId, DateTimeOffset fromUtcInclusive, DateTimeOffset toUtcExclusive, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IStockAvailabilityChecker
{
    Task<bool> IsAvailableAsync(Guid storeId, Guid productId, decimal requestedQuantity, CancellationToken ct = default);
}
