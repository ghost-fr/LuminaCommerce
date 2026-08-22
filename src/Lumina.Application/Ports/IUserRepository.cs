using Lumina.Domain.Identity;

namespace Lumina.Application.Ports;

/// <summary>
/// Port for user persistence. Implemented in Lumina.Infrastructure against EF Core;
/// Application layer (and its tests) depend only on this interface, never on
/// LuminaDbContext directly — keeps AuthService unit-testable without a real database.
/// </summary>
public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(Guid tenantId, string username, CancellationToken ct = default);
    Task<User?> FindByIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IRoleRepository
{
    Task<IReadOnlyList<Role>> GetByIdsAsync(IEnumerable<Guid> roleIds, CancellationToken ct = default);
}

/// <summary>
/// Resolves which Tenant the running process belongs to. In the single-tenant
/// desktop POS deployment (this project's primary target per the blueprint's SMB
/// focus) this reads a TenantId from local config, set once at install time. A
/// future multi-tenant hosted mode would instead resolve this per-request.
/// </summary>
public interface ICurrentTenantProvider
{
    Guid TenantId { get; }
}

public interface IStoreRepository
{
    Task<Store?> FindByIdAsync(Guid storeId, CancellationToken ct = default);

    /// <summary>Returns true if the user's tenant has at least one store and the given
    /// storeId belongs to it — used to validate LoginRequest.RequestedStoreId.</summary>
    Task<bool> BelongsToTenantAsync(Guid storeId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Default store to select when login doesn't specify one — e.g. the
    /// tenant's first active store. Single-store tenants (the common SMB case) skip
    /// store selection UI entirely this way.</summary>
    Task<Store?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default);
}
