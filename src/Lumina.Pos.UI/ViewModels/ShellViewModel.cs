using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Capability-gated shell. Each module has a dedicated ViewModel with mock data
/// until backend read models / contracts are available.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    public const string CapPosOperateRegister = "pos.operate_register";
    public const string CapAdminViewReports = "admin.view_reports";
    public const string CapAdminManageCatalogue = "admin.manage_catalogue";
    public const string CapStockAdjust = "stock.adjust";
    public const string CapFiscalManageVeriFactu = "fiscal.manage_verifactu";
    public const string CapAdminManageUsers = "admin.manage_users";

    private readonly IAuthService _auth;
    private readonly Action _onLoggedOut;

    public IUserSession Session { get; }

    public string DisplayName => Session.DisplayName;
    public string Username => Session.Username;
    public string UserInitial =>
        string.IsNullOrWhiteSpace(DisplayName) ? "?" : DisplayName.Trim()[0].ToString().ToUpperInvariant();

    public bool CanOperatePos => Session.HasCapability(CapPosOperateRegister);
    public bool CanViewReports => Session.HasCapability(CapAdminViewReports);
    public bool CanManageCatalogue => Session.HasCapability(CapAdminManageCatalogue);
    public bool CanAdjustStock => Session.HasCapability(CapStockAdjust);
    public bool CanManageFiscal => Session.HasCapability(CapFiscalManageVeriFactu);
    public bool CanManageUsers => Session.HasCapability(CapAdminManageUsers);

    // Module VMs (created once per session)
    public PosModuleViewModel Pos { get; } = new();
    public ProductsModuleViewModel Products { get; } = new();
    public CustomersModuleViewModel Customers { get; } = new();
    public SalesModuleViewModel Sales { get; } = new();
    public StockModuleViewModel Stock { get; } = new();
    public PurchasesModuleViewModel Purchases { get; } = new();
    public CashModuleViewModel Cash { get; } = new();
    public ReportsModuleViewModel Reports { get; } = new();
    public UsersModuleViewModel Users { get; } = new();
    public SettingsModuleViewModel Settings { get; } = new();
    public VeriFactuModuleViewModel VeriFactu { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInicio))]
    [NotifyPropertyChangedFor(nameof(IsTpv))]
    [NotifyPropertyChangedFor(nameof(IsVentas))]
    [NotifyPropertyChangedFor(nameof(IsClientes))]
    [NotifyPropertyChangedFor(nameof(IsArticulos))]
    [NotifyPropertyChangedFor(nameof(IsStock))]
    [NotifyPropertyChangedFor(nameof(IsCompras))]
    [NotifyPropertyChangedFor(nameof(IsCaja))]
    [NotifyPropertyChangedFor(nameof(IsInformes))]
    [NotifyPropertyChangedFor(nameof(IsUsuarios))]
    [NotifyPropertyChangedFor(nameof(IsConfig))]
    [NotifyPropertyChangedFor(nameof(IsVerifactu))]
    [NotifyPropertyChangedFor(nameof(IsAyuda))]
    [NotifyPropertyChangedFor(nameof(IsNavInicio))]
    [NotifyPropertyChangedFor(nameof(IsNavTpv))]
    [NotifyPropertyChangedFor(nameof(IsNavVentas))]
    [NotifyPropertyChangedFor(nameof(IsNavClientes))]
    [NotifyPropertyChangedFor(nameof(IsNavArticulos))]
    [NotifyPropertyChangedFor(nameof(IsNavStock))]
    [NotifyPropertyChangedFor(nameof(IsNavCompras))]
    [NotifyPropertyChangedFor(nameof(IsNavInformes))]
    [NotifyPropertyChangedFor(nameof(IsNavVerifactu))]
    [NotifyPropertyChangedFor(nameof(IsNavCaja))]
    [NotifyPropertyChangedFor(nameof(IsNavConfig))]
    [NotifyPropertyChangedFor(nameof(IsNavUsuarios))]
    [NotifyPropertyChangedFor(nameof(IsNavAyuda))]
    private string _currentSection = "inicio";

    public bool IsInicio => CurrentSection == "inicio";
    public bool IsTpv => CurrentSection == "tpv";
    public bool IsVentas => CurrentSection == "ventas";
    public bool IsClientes => CurrentSection == "clientes";
    public bool IsArticulos => CurrentSection == "articulos";
    public bool IsStock => CurrentSection == "stock";
    public bool IsCompras => CurrentSection == "compras";
    public bool IsCaja => CurrentSection == "caja";
    public bool IsInformes => CurrentSection == "informes";
    public bool IsUsuarios => CurrentSection == "usuarios";
    public bool IsConfig => CurrentSection == "config";
    public bool IsVerifactu => CurrentSection == "verifactu";
    public bool IsAyuda => CurrentSection == "ayuda";

    public bool IsNavInicio => IsInicio;
    public bool IsNavTpv => IsTpv;
    public bool IsNavVentas => IsVentas;
    public bool IsNavClientes => IsClientes;
    public bool IsNavArticulos => IsArticulos;
    public bool IsNavStock => IsStock;
    public bool IsNavCompras => IsCompras;
    public bool IsNavInformes => IsInformes;
    public bool IsNavVerifactu => IsVerifactu;
    public bool IsNavCaja => IsCaja;
    public bool IsNavConfig => IsConfig;
    public bool IsNavUsuarios => IsUsuarios;
    public bool IsNavAyuda => IsAyuda;

    public ShellViewModel(IAuthService auth, Action onLoggedOut)
    {
        _auth = auth;
        _onLoggedOut = onLoggedOut;
        Session = auth.CurrentSession
            ?? throw new InvalidOperationException("Shell requires an authenticated session.");
    }

    [RelayCommand]
    private void Navigate(string section)
    {
        if (string.IsNullOrWhiteSpace(section)) return;

        if (section is "tpv" or "caja" && !CanOperatePos) return;
        if (section is "articulos" && !CanManageCatalogue) return;
        if (section is "stock" or "compras" && !CanAdjustStock && section == "stock") return;
        if (section is "informes" or "ventas" && !CanViewReports) return;
        if (section is "verifactu" && !CanManageFiscal) return;
        if (section is "usuarios" or "config" && !CanManageUsers) return;

        // stock: allow if adjust OR reports (view levels)
        if (section == "stock" && !CanAdjustStock && !CanViewReports) return;

        CurrentSection = section;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        _onLoggedOut();
    }
}
