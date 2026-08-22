using Lumina.Application.Auth;
using Lumina.Application.Ports;
using Lumina.Contracts.Auth;
using Lumina.Domain.Identity;
using Xunit;

namespace Lumina.Application.Tests.Auth;

public class AuthServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid StoreId = Guid.NewGuid();

    private sealed class FakeUserRepository : IUserRepository
    {
        public readonly List<User> Users = new();
        public Task<User?> FindByUsernameAsync(Guid tenantId, string username, CancellationToken ct = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.TenantId == tenantId && u.Username == username));
        public Task<User?> FindByIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));
        public Task AddAsync(User user, CancellationToken ct = default) { Users.Add(user); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public readonly List<Role> Roles = new();
        public Task<IReadOnlyList<Role>> GetByIdsAsync(IEnumerable<Guid> roleIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Role>>(Roles.Where(r => roleIds.Contains(r.Id)).ToList());
    }

    private sealed class FakeStoreRepository : IStoreRepository
    {
        public Store? DefaultStore;
        public Task<Store?> FindByIdAsync(Guid storeId, CancellationToken ct = default) =>
            Task.FromResult(DefaultStore?.Id == storeId ? DefaultStore : null);
        public Task<bool> BelongsToTenantAsync(Guid storeId, Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(DefaultStore?.Id == storeId && DefaultStore.TenantId == tenantId);
        public Task<Store?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(DefaultStore?.TenantId == tenantId ? DefaultStore : null);
    }

    private sealed class FakeTenantProvider : ICurrentTenantProvider
    {
        public Guid TenantId { get; init; }
    }

    private static (AuthService svc, FakeUserRepository users, FakeRoleRepository roles, FakeStoreRepository stores)
        BuildSut()
    {
        var users = new FakeUserRepository();
        var roles = new FakeRoleRepository();
        var stores = new FakeStoreRepository
        {
            DefaultStore = new Store(StoreId, TenantId, "Main Store", "PerStore")
        };
        var tenantProvider = new FakeTenantProvider { TenantId = TenantId };
        var svc = new AuthService(users, roles, stores, tenantProvider);
        return (svc, users, roles, stores);
    }

    [Fact]
    public async Task LoginAsync_Succeeds_WithValidCredentialsAndDefaultStore()
    {
        var (svc, users, roles, _) = BuildSut();
        var role = new Role(Guid.NewGuid(), TenantId, "Cashier", new[] { Capabilities.PosOperateRegister });
        roles.Roles.Add(role);
        var user = new User(Guid.NewGuid(), TenantId, "alice", "Alice", PasswordHasher.Hash("correct-horse"));
        user.AssignRole(role.Id);
        users.Users.Add(user);

        var result = await svc.LoginAsync(new LoginRequest("alice", "correct-horse"));

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.NotNull(result.Session);
        Assert.Equal(StoreId, result.Session!.StoreId);
        Assert.True(result.Session.HasCapability(Capabilities.PosOperateRegister));
        Assert.Same(result.Session, svc.CurrentSession);
    }

    [Fact]
    public async Task LoginAsync_Fails_WithWrongPassword()
    {
        var (svc, users, _, _) = BuildSut();
        users.Users.Add(new User(Guid.NewGuid(), TenantId, "alice", "Alice", PasswordHasher.Hash("correct-horse")));

        var result = await svc.LoginAsync(new LoginRequest("alice", "wrong-password"));

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Session);
        Assert.Null(svc.CurrentSession);
    }

    [Fact]
    public async Task LoginAsync_Fails_WithUnknownUsername()
    {
        var (svc, _, _, _) = BuildSut();

        var result = await svc.LoginAsync(new LoginRequest("nobody", "whatever"));

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }

    [Fact]
    public async Task LoginAsync_Fails_WhenUserInactive()
    {
        var (svc, users, _, _) = BuildSut();
        var user = new User(Guid.NewGuid(), TenantId, "alice", "Alice", PasswordHasher.Hash("correct-horse"));
        user.Deactivate();
        users.Users.Add(user);

        var result = await svc.LoginAsync(new LoginRequest("alice", "correct-horse"));

        Assert.Equal(LoginStatus.UserInactive, result.Status);
    }

    [Fact]
    public async Task LogoutAsync_ClearsCurrentSession()
    {
        var (svc, users, roles, _) = BuildSut();
        var user = new User(Guid.NewGuid(), TenantId, "alice", "Alice", PasswordHasher.Hash("correct-horse"));
        users.Users.Add(user);
        await svc.LoginAsync(new LoginRequest("alice", "correct-horse"));

        await svc.LogoutAsync();

        Assert.Null(svc.CurrentSession);
    }
}
