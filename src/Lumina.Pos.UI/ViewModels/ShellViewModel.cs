using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Catalogue;
using Lumina.Contracts.Pos;
using Lumina.Contracts.Stock;
using Lumina.Domain.Identity;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// GesVent-shaped shell: left explorer modules, capability-gated live screens.
/// Unimplemented GesVent modules open an honest placeholder — they do not fake data.
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
        IStockService stock)
    {
        _auth = auth;
        _pos = pos;
        _catalogue = catalogue;
        _stock = stock;
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
        CurrentContent = new PosCartViewModel(_pos, _auth.CurrentSession, registerId: null);
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

/// <summary>Honest GesVent module map when the Lumina use-case is not built yet.</summary>
public sealed class ModulePlaceholderViewModel
{
    public string ModuleName { get; }
    public string GesVentKey { get; }
    public string Summary { get; }
    public IReadOnlyList<string> Functions { get; }

    public ModulePlaceholderViewModel(string moduleName, string gesVentKey, string summary, IReadOnlyList<string> functions)
    {
        ModuleName = moduleName;
        GesVentKey = gesVentKey;
        Summary = summary;
        Functions = functions;
    }

    public static ModulePlaceholderViewModel Compras() => new(
        "Compras",
        "COM",
        "Pedidos, albaranes y facturas de proveedor. En GesVent: COMPED / COMALB / COMFAC. Lumina still needs the purchasing document state machine before this screen can write data.",
        [
            "Propuestas de pedido (COMPRO)",
            "Pedidos a proveedor (COMPED)",
            "Albaranes de compra (COMALB)",
            "Facturas de compra (COMFAC)",
            "Informes de compras (COMINF)",
        ]);

    public static ModulePlaceholderViewModel Ventas() => new(
        "Ventas",
        "VEN",
        "Documentos de venta mayor (no el ticket de caja). En GesVent: VENPED / VENALB / VENFAC. POS tickets already live under TPV.",
        [
            "Presupuestos (VENPRE)",
            "Pedidos de cliente (VENPED)",
            "Albaranes de venta (VENALB)",
            "Facturas de venta (VENFAC)",
            "Informes de ventas (VENINF)",
        ]);

    public static ModulePlaceholderViewModel Informes() => new(
        "Informes y gráficas",
        "INF",
        "Read-models: diario, IVA, formas de pago, visor de tickets. Needs reporting queries; do not copy GesVent DataSets into the UI.",
        [
            "Informe diario (INFTIE)",
            "Visor de tickets / cuadres (INFVTP)",
            "Ventas por artículo / familia / IVA",
            "Cuadrante de cajas (INFCCA)",
            "VeriFactu chain verification",
        ]);

    public static ModulePlaceholderViewModel Tablas() => new(
        "Tablas generales",
        "TAB",
        "Maestros: clientes, proveedores, tiendas, formas de pago, medidas. Catalogue products already have a live screen under Artículos.",
        [
            "Clientes (TABCLI)",
            "Proveedores (TABPRO)",
            "Tiendas (TABTIE)",
            "Formas de pago (TABFOR)",
            "Centros de stock (TABCEN)",
        ]);

    public static ModulePlaceholderViewModel Sistema() => new(
        "Sistema",
        "SIS",
        "Empresas, usuarios, configuración, backups, remoting. Today: login + capabilities. Config is appsettings / env, not CONFIG.XML.",
        [
            "Empresa activa (SISEMP / SISCEM)",
            "Usuarios y permisos (SISUSU)",
            "Configuración (SISCFG)",
            "Backups (SISBAK)",
            "Importar / exportar maestros y ventas",
        ]);
}
