using Lumina.Application.Ports;
using Lumina.Contracts.Auth;

namespace Lumina.Application.Auth;

/// <summary>
/// Concrete implementation of Lumina.Contracts.Auth.IAuthService. Registered as a
/// scoped/singleton service at the composition root (App.axaml.cs in each UI project).
/// UI code must depend on IAuthService, never on this class directly.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IStoreRepository _stores;
    private readonly ICurrentTenantProvider _tenantProvider;

    private IUserSession? _currentSession;
    public IUserSession? CurrentSession => _currentSession;

    public AuthService(
        IUserRepository users, IRoleRepository roles, IStoreRepository stores,
        ICurrentTenantProvider tenantProvider)
    {
        _users = users;
        _roles = roles;
        _stores = stores;
        _tenantProvider = tenantProvider;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        // NOTE: username lookup is tenant-scoped in the real multi-tenant deployment.
        // For the single-tenant desktop POS case (the common SMB target per the
        // blueprint's SMB focus) ICurrentTenantProvider resolves against the local
        // install's one tenant, read from config. Revisit if/when a multi-tenant
        // hosted mode is added.
        var localTenantId = _tenantProvider.TenantId;

        var user = await _users.FindByUsernameAsync(localTenantId, request.Username, ct);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return new LoginResult(LoginStatus.InvalidCredentials, null, "Invalid username or password.");

        if (!user.IsActive)
            return new LoginResult(LoginStatus.UserInactive, null, "This user account is inactive.");

        var storeId = request.RequestedStoreId;
        if (storeId is null)
        {
            var defaultStore = await _stores.GetDefaultForTenantAsync(user.TenantId, ct);
            if (defaultStore is null)
                return new LoginResult(LoginStatus.StoreNotAssigned, null, "No store is configured for this account.");
            storeId = defaultStore.Id;
        }
        else if (!await _stores.BelongsToTenantAsync(storeId.Value, user.TenantId, ct))
        {
            return new LoginResult(LoginStatus.StoreNotAssigned, null, "Selected store is not available to this account.");
        }

        var roles = await _roles.GetByIdsAsync(user.RoleIds, ct);
        var capabilities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
            foreach (var cap in role.Capabilities)
                capabilities.Add(cap);

        _currentSession = new UserSession(
            user.Id, user.TenantId, storeId.Value, user.DisplayName, user.Username, capabilities);

        return new LoginResult(LoginStatus.Success, _currentSession, null);
    }

    public Task LogoutAsync(CancellationToken ct = default)
    {
        _currentSession = null;
        return Task.CompletedTask;
    }
}

