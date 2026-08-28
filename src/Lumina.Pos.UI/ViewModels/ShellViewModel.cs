using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Domain.Identity;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Top-level shell for the POS window. Switches between the login screen and the
/// main (capability-gated) content based on <see cref="IAuthService.CurrentSession"/>.
/// UI never tracks a separate isLoggedIn flag — the session is the single source of truth.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    [ObservableProperty]
    private object? _currentContent;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private bool _isLoggedIn;

    public LoginViewModel Login { get; }

    public ShellViewModel(IAuthService auth)
    {
        _auth = auth;
        Login = new LoginViewModel(auth, OnLoginSucceeded);
        ShowLogin();
    }

    private void OnLoginSucceeded()
    {
        var session = _auth.CurrentSession;
        if (session is null)
        {
            ShowLogin();
            return;
        }

        IsLoggedIn = true;
        StatusText = $"{session.DisplayName}  ·  Store {session.StoreId.ToString()[..8]}…";

        // For now show a simple placeholder main area. Phase 3 cart/sale UI will
        // replace this content. Capability checks are already available for menus.
        CurrentContent = new MainPlaceholderViewModel(session);
    }

    private void ShowLogin()
    {
        IsLoggedIn = false;
        StatusText = null;
        CurrentContent = Login;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        ShowLogin();
    }

    /// <summary>Convenience for XAML / code that needs capability checks.</summary>
    public bool CanOperateRegister =>
        _auth.CurrentSession?.HasCapability(Capabilities.PosOperateRegister) == true;

    public bool CanOverridePrice =>
        _auth.CurrentSession?.HasCapability(Capabilities.PosOverridePrice) == true;

    public bool CanVoidSale =>
        _auth.CurrentSession?.HasCapability(Capabilities.PosVoidSale) == true;
}

/// <summary>
/// Temporary placeholder shown after successful login until the real POS cart
/// screen (Phase 3 UI) is built.
/// </summary>
public partial class MainPlaceholderViewModel : ObservableObject
{
    public string Greeting { get; }
    public string CapabilitiesSummary { get; }

    public MainPlaceholderViewModel(IUserSession session)
    {
        Greeting = $"Welcome, {session.DisplayName}";
        CapabilitiesSummary = session.Capabilities.Count == 0
            ? "(no capabilities)"
            : string.Join(", ", session.Capabilities.OrderBy(c => c));
    }
}
