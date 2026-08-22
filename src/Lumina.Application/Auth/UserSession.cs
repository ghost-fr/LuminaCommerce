using Lumina.Contracts.Auth;

namespace Lumina.Application.Auth;

internal sealed class UserSession : IUserSession
{
    public Guid UserId { get; }
    public Guid TenantId { get; }
    public Guid StoreId { get; }
    public string DisplayName { get; }
    public string Username { get; }
    public IReadOnlySet<string> Capabilities { get; }

    public UserSession(
        Guid userId, Guid tenantId, Guid storeId,
        string displayName, string username, IReadOnlySet<string> capabilities)
    {
        UserId = userId;
        TenantId = tenantId;
        StoreId = storeId;
        DisplayName = displayName;
        Username = username;
        Capabilities = capabilities;
    }

    public bool HasCapability(string capability) => Capabilities.Contains(capability);
}
