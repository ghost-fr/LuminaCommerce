using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Catalogue;
using Lumina.Contracts.Pos;
using Lumina.Contracts.Stock;
using Lumina.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Top-level shell: login, then capability-gated nav (Register, Catalogue, Stock).
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly IPosSaleService _pos;
    private readonly IProductCatalogueService _catalogue;
    private readonly IStockService _stock;

    [ObservableProperty] private object? _currentContent;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private string _activeNav = "register";

    public LoginViewModel Login { get; }

    public bool CanOperateRegister =>
        _auth.CurrentSession?.HasCapability(Capabilities.PosOperateRegister) == true;

    public bool CanBrowseCatalogue =>
        _auth.CurrentSession?.HasCapability(Capabilities.AdminManageCatalogue) == true
        || CanOperateRegister;

    public bool CanManageStock =>
        _auth.CurrentSession?.HasCapability(Capabilities.StockAdjust) == true
        || _auth.CurrentSession?.HasCapability(Capabilities.StockTransfer) == true;

    public ShellViewModel(
        IAuthService auth,
        IPosSaleService pos,
        IProductCatalogueService catalogue,
        IStockService stock)
    {
        _auth = auth;
        _pos = pos;
        _catalogue = catalogue;
        _stock = stock;
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

        // Prefer register if allowed; otherwise first available screen.
        if (CanOperateRegister)
            NavigateRegister();
        else if (CanBrowseCatalogue)
            NavigateCatalogue();
        else if (CanManageStock)
            NavigateStock();
        else
        {
            CurrentContent = new MainPlaceholderViewModel(session);
            StatusText += "  ·  (no POS/catalogue/stock capabilities)";
            ActiveNav = "none";
        }
    }

    private void ShowLogin()
    {
        IsLoggedIn = false;
        StatusText = null;
        ActiveNav = "login";
        CurrentContent = Login;
    }

    [RelayCommand]
    private void NavigateRegister()
    {
        if (!CanOperateRegister || _auth.CurrentSession is null) return;
        ActiveNav = "register";
        CurrentContent = new PosCartViewModel(_pos, _auth.CurrentSession, registerId: null);
    }

    [RelayCommand]
    private void NavigateCatalogue()
    {
        if (!CanBrowseCatalogue) return;
        ActiveNav = "catalogue";
        CurrentContent = new CatalogueViewModel(_catalogue);
    }

    [RelayCommand]
    private void NavigateStock()
    {
        if (!CanManageStock || _auth.CurrentSession is null) return;
        ActiveNav = "stock";
        CurrentContent = new StockViewModel(_stock, _auth.CurrentSession);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        ShowLogin();
    }
}

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
