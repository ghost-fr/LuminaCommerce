namespace Lumina.Contracts.Auth;

public record LoginRequest(string Username, string Password, Guid? RequestedStoreId = null);

public enum LoginStatus
{
    Success,
    InvalidCredentials,
    UserInactive,
    StoreNotAssigned
}

public record LoginResult(LoginStatus Status, IUserSession? Session, string? FailureReason);

/// <summary>
/// Represents the authenticated session for the lifetime of the app process (or until
/// logout). Combines what the blueprint calls TenantContext + StoreContext + UserSession
/// into one object the UI reads from — UI never constructs this, only receives it from
/// IAuthService and passes it implicitly via DI scope to application service calls.
/// </summary>
public interface IUserSession
{
    Guid UserId { get; }
    Guid TenantId { get; }
    Guid StoreId { get; }
    string DisplayName { get; }
    string Username { get; }
    IReadOnlySet<string> Capabilities { get; }

    /// <summary>Convenience check UI uses to show/hide capability-gated menu items and buttons.
    /// Must be a pure, side-effect-free check against the already-loaded capability set —
    /// never triggers a network/DB call, so it's safe to call from XAML bindings/converters.</summary>
    bool HasCapability(string capability);
}

/// <summary>
/// Entry point for authentication. UI (Grok) depends only on this interface; the
/// concrete implementation (Lumina.Application.Auth.AuthService) is resolved via DI
/// at the composition root.
/// </summary>
public interface IAuthService
{
    /// <summary>Null when no one is logged in. UI nav shell should treat null as
    /// "show login screen", non-null as "show capability-filtered main nav".</summary>
    IUserSession? CurrentSession { get; }

    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
}
