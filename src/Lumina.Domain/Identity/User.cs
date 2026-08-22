namespace Lumina.Domain.Identity;

/// <summary>
/// A person who can authenticate into Lumina. Password storage is a hash + salt
/// produced by Lumina.Application's PasswordHasher — this aggregate never sees
/// plaintext passwords and has no knowledge of the hashing algorithm.
/// </summary>
public class User
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Username { get; private set; }
    public string DisplayName { get; private set; }
    public string PasswordHash { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<Guid> _roleIds = new();
    public IReadOnlyList<Guid> RoleIds => _roleIds;

    private User()
    {
        Username = string.Empty;
        DisplayName = string.Empty;
        PasswordHash = string.Empty;
    }

    public User(Guid id, Guid tenantId, string username, string displayName, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash is required.", nameof(passwordHash));

        Id = id;
        TenantId = tenantId;
        Username = username;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName;
        PasswordHash = passwordHash;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void AssignRole(Guid roleId)
    {
        if (!_roleIds.Contains(roleId))
            _roleIds.Add(roleId);
    }

    public void RevokeRole(Guid roleId) => _roleIds.Remove(roleId);

    public void Deactivate() => IsActive = false;

    public void ChangePasswordHash(string newHash)
    {
        if (string.IsNullOrWhiteSpace(newHash))
            throw new ArgumentException("PasswordHash is required.", nameof(newHash));
        PasswordHash = newHash;
    }
}

/// <summary>
/// A named bundle of capabilities, scoped to a Tenant. Roles are assigned to Users;
/// a User's effective capability set is the union of all their Roles' capabilities.
/// </summary>
public class Role
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }

    private readonly HashSet<string> _capabilities = new(StringComparer.Ordinal);
    public IReadOnlySet<string> Capabilities => _capabilities;

    private Role() { Name = string.Empty; }

    public Role(Guid id, Guid tenantId, string name, IEnumerable<string>? capabilities = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name is required.", nameof(name));

        Id = id;
        TenantId = tenantId;
        Name = name;
        if (capabilities != null)
            foreach (var c in capabilities) _capabilities.Add(c);
    }

    public void Grant(string capability) => _capabilities.Add(capability);
    public void Revoke(string capability) => _capabilities.Remove(capability);
}

/// <summary>
/// Well-known capability strings. Deliberately plain strings (not an enum) so new
/// capabilities can be added by later modules (e.g. Commercial Documents, Devices)
/// without every consumer recompiling against a shared enum. Keep names stable once
/// shipped — they may end up referenced in seeded data / audit logs.
/// </summary>
public static class Capabilities
{
    public const string PosOperateRegister = "pos.operate_register";
    public const string PosOverridePrice = "pos.override_price";
    public const string PosVoidSale = "pos.void_sale";
    public const string PosIssueRefund = "pos.issue_refund";

    public const string StockAdjust = "stock.adjust";
    public const string StockTransfer = "stock.transfer";

    public const string AdminManageUsers = "admin.manage_users";
    public const string AdminManageCatalogue = "admin.manage_catalogue";
    public const string AdminManagePricing = "admin.manage_pricing";
    public const string AdminViewReports = "admin.view_reports";

    public const string FiscalManageVeriFactu = "fiscal.manage_verifactu";
}
