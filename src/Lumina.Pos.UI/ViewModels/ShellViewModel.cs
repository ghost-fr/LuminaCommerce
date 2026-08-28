using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Pos;
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
    private readonly IPosSaleService _pos;

    [ObservableProperty]
    private object? _currentContent;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private bool _isLoggedIn;

    public LoginViewModel Login { get; }

    public ShellViewModel(IAuthService auth, IPosSaleService pos)
    {
        _auth = auth;
        _pos = pos;
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

        // Capability gate: only operators with pos.operate_register get the cart.
        if (!session.HasCapability(Capabilities.PosOperateRegister))
        {
            CurrentContent = new MainPlaceholderViewModel(session);
            StatusText += "  ·  (no pos.operate_register)";
            return;
        }

        // RegisterId: until open-register lands on the contract, pass Guid.Empty.
        // Complete sale will reject with RegisterNotOpen until SeedDev / backend
        // provides an open register id (or a future CreateCart+OpenRegister API).
        CurrentContent = new PosCartViewModel(_pos, session, registerId: null);
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

    public bool CanOperateRegister =>
        _auth.CurrentSession?.HasCapability(Capabilities.PosOperateRegister) == true;

    public bool CanOverridePrice =>
        _auth.CurrentSession?.HasCapability(Capabilities.PosOverridePrice) == true;

    public bool CanVoidSale =>
        _auth.CurrentSession?.HasCapability(Capabilities.PosVoidSale) == true;
}

/// <summary>
/// Shown when the user lacks pos.operate_register (or as fallback).
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
