using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Catalogue;
using Lumina.Contracts.Devices;
using Lumina.Contracts.Pos;
using Lumina.Contracts.Stock;
using Lumina.Domain.Identity;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// GesVent-shaped shell: left explorer modules, capability-gated live screens.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly IPosSaleService _pos;
    private readonly IProductCatalogueService _catalogue;
    private readonly IStockService _stock;
    private readonly IReceiptPrinter _printer;
    private readonly IScaleService _scale;

    [ObservableProperty] private object? _currentContent;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private string _activeNav = "register";
    [ObservableProperty] private string _headerTitle = "TPV";

    public LoginViewModel Login { get; }

    public bool CanOperateRegister =>
        _auth.CurrentSession?.HasCapability(Capabilities.PosOperateRegister) == true;

    public bool CanBrowseCatalogue =>
        _auth.CurrentSession?.HasCapability(Capabilities.AdminManageCatalogue) == true
        || CanOperateRegister;

    public bool CanManageStock =>
        _auth.CurrentSession?.HasCapability(Capabilities.StockAdjust) == true
        || _auth.CurrentSession?.HasCapability(Capabilities.StockTransfer) == true;

    public bool IsTpvActive => ActiveNav == "register";
    public bool IsArticulosActive => ActiveNav == "catalogue";
    public bool IsAlmacenActive => ActiveNav == "stock";
    public bool IsComprasActive => ActiveNav == "compras";
    public bool IsVentasActive => ActiveNav == "ventas";
    public bool IsInformesActive => ActiveNav == "informes";
    public bool IsTablasActive => ActiveNav == "tablas";
    public bool IsSistemaActive => ActiveNav == "sistema";
    public bool IsHomeActive => ActiveNav == "none";

    public ShellViewModel(
        IAuthService auth,
        IPosSaleService pos,
        IProductCatalogueService catalogue,
        IStockService stock,
        IReceiptPrinter printer,
        IScaleService scale)
    {
        _auth = auth;
        _pos = pos;
        _catalogue = catalogue;
        _stock = stock;
        _printer = printer;
        _scale = scale;
        Login = new LoginViewModel(auth, OnLoginSucceeded);
        ShowLogin();
    }

    partial void OnActiveNavChanged(string value)
    {
        OnPropertyChanged(nameof(IsTpvActive));
        OnPropertyChanged(nameof(IsArticulosActive));
        OnPropertyChanged(nameof(IsAlmacenActive));
        OnPropertyChanged(nameof(IsComprasActive));
        OnPropertyChanged(nameof(IsVentasActive));
        OnPropertyChanged(nameof(IsInformesActive));
        OnPropertyChanged(nameof(IsTablasActive));
        OnPropertyChanged(nameof(IsSistemaActive));
        OnPropertyChanged(nameof(IsHomeActive));
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
        StatusText = $"{session.DisplayName}  ·  tienda {session.StoreId.ToString()[..8]}…";

        if (CanOperateRegister)
            NavigateRegister();
        else if (CanBrowseCatalogue)
            NavigateCatalogue();
        else if (CanManageStock)
            NavigateStock();
        else
        {
            CurrentContent = new MainPlaceholderViewModel(session);
            HeaderTitle = "Inicio";
            StatusText += "  ·  sin TPV / catálogo / stock";
            ActiveNav = "none";
        }
    }

    private void ShowLogin()
    {
        IsLoggedIn = false;
        StatusText = null;
        ActiveNav = "login";
        HeaderTitle = "Acceso";
        CurrentContent = Login;
    }

    [RelayCommand]
    private void NavigateRegister()
    {
        if (!CanOperateRegister || _auth.CurrentSession is null) return;
        ActiveNav = "register";
        HeaderTitle = "Caja / TPV";
        CurrentContent = new PosCartViewModel(_pos, _auth.CurrentSession, _printer, _scale, registerId: null);
    }

    [RelayCommand]
    private void NavigateCatalogue()
    {
        if (!CanBrowseCatalogue) return;
        ActiveNav = "catalogue";
        HeaderTitle = "Artículos";
        CurrentContent = new CatalogueViewModel(_catalogue);
    }

    [RelayCommand]
    private void NavigateStock()
    {
        if (!CanManageStock || _auth.CurrentSession is null) return;
        ActiveNav = "stock";
        HeaderTitle = "Almacén";
        CurrentContent = new StockViewModel(_stock, _auth.CurrentSession);
    }

    [RelayCommand]
    private void NavigateCompras()
    {
        ActiveNav = "compras";
        HeaderTitle = "Compras";
        CurrentContent = ModulePlaceholderViewModel.Compras();
    }

    [RelayCommand]
    private void NavigateVentas()
    {
        ActiveNav = "ventas";
        HeaderTitle = "Ventas";
        CurrentContent = ModulePlaceholderViewModel.Ventas();
    }

    [RelayCommand]
    private void NavigateInformes()
    {
        ActiveNav = "informes";
        HeaderTitle = "Informes y gráficas";
        CurrentContent = ModulePlaceholderViewModel.Informes();
    }

    [RelayCommand]
    private void NavigateTablas()
    {
        ActiveNav = "tablas";
        HeaderTitle = "Tablas generales";
        CurrentContent = ModulePlaceholderViewModel.Tablas();
    }

    [RelayCommand]
    private void NavigateSistema()
    {
        ActiveNav = "sistema";
        HeaderTitle = "Sistema";
        CurrentContent = ModulePlaceholderViewModel.Sistema();
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
        Greeting = $"Bienvenido, {session.DisplayName}";
        CapabilitiesSummary = session.Capabilities.Count == 0
            ? "(sin permisos)"
            : string.Join(", ", session.Capabilities.OrderBy(c => c));
    }
}
