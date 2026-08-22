using Lumina.Application.Ports;
using Lumina.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly LuminaDbContext _db;
    public UserRepository(LuminaDbContext db) => _db = db;

    public Task<User?> FindByUsernameAsync(Guid tenantId, string username, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Username == username, ct);

    public Task<User?> FindByIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await _db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);
}

public class RoleRepository : IRoleRepository
{
    private readonly LuminaDbContext _db;
    public RoleRepository(LuminaDbContext db) => _db = db;

    public async Task<IReadOnlyList<Role>> GetByIdsAsync(IEnumerable<Guid> roleIds, CancellationToken ct = default)
    {
        var ids = roleIds.ToList();
        if (ids.Count == 0) return Array.Empty<Role>();
        return await _db.Roles.Where(r => ids.Contains(r.Id)).ToListAsync(ct);
    }
}

public class StoreRepository : IStoreRepository
{
    private readonly LuminaDbContext _db;
    public StoreRepository(LuminaDbContext db) => _db = db;

    public Task<Store?> FindByIdAsync(Guid storeId, CancellationToken ct = default) =>
        _db.Stores.FirstOrDefaultAsync(s => s.Id == storeId, ct);

    public Task<bool> BelongsToTenantAsync(Guid storeId, Guid tenantId, CancellationToken ct = default) =>
        _db.Stores.AnyAsync(s => s.Id == storeId && s.TenantId == tenantId, ct);

    public Task<Store?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Stores.Where(s => s.TenantId == tenantId && s.IsActive)
            .OrderBy(s => s.Id) // deterministic; revisit if stores gain a display-order field
            .FirstOrDefaultAsync(ct);
}
