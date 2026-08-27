using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Capability-gated main shell. Nav active state drives CSS-like .active styles in XAML.
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInicio))]
    [NotifyPropertyChangedFor(nameof(IsTpv))]
    [NotifyPropertyChangedFor(nameof(IsPlaceholder))]
    [NotifyPropertyChangedFor(nameof(PlaceholderTitle))]
    [NotifyPropertyChangedFor(nameof(PlaceholderHint))]
    [NotifyPropertyChangedFor(nameof(IsNavInicio))]
    [NotifyPropertyChangedFor(nameof(IsNavTpv))]
    [NotifyPropertyChangedFor(nameof(IsNavVentas))]
    [NotifyPropertyChangedFor(nameof(IsNavClientes))]
    [NotifyPropertyChangedFor(nameof(IsNavArticulos))]
    [NotifyPropertyChangedFor(nameof(IsNavStock))]
    [NotifyPropertyChangedFor(nameof(IsNavInformes))]
    [NotifyPropertyChangedFor(nameof(IsNavVerifactu))]
    [NotifyPropertyChangedFor(nameof(IsNavCaja))]
    [NotifyPropertyChangedFor(nameof(IsNavConfig))]
    [NotifyPropertyChangedFor(nameof(IsNavUsuarios))]
    [NotifyPropertyChangedFor(nameof(IsNavAyuda))]
    private string _currentSection = "inicio";

    public bool IsInicio => CurrentSection == "inicio";
    public bool IsTpv => CurrentSection == "tpv";
    public bool IsPlaceholder => !IsInicio && !IsTpv;

    public bool IsNavInicio => CurrentSection == "inicio";
    public bool IsNavTpv => CurrentSection == "tpv";
    public bool IsNavVentas => CurrentSection == "ventas";
    public bool IsNavClientes => CurrentSection == "clientes";
    public bool IsNavArticulos => CurrentSection == "articulos";
    public bool IsNavStock => CurrentSection == "stock";
    public bool IsNavInformes => CurrentSection == "informes";
    public bool IsNavVerifactu => CurrentSection == "verifactu";
    public bool IsNavCaja => CurrentSection == "caja";
    public bool IsNavConfig => CurrentSection == "config";
    public bool IsNavUsuarios => CurrentSection == "usuarios";
    public bool IsNavAyuda => CurrentSection == "ayuda";

    public string PlaceholderTitle => CurrentSection switch
    {
        "ventas" => "Ventas",
        "clientes" => "Clientes",
        "articulos" => "Artículos",
        "stock" => "Stock",
        "informes" => "Informes",
        "verifactu" => "VeriFactu",
        "caja" => "Caja",
        "config" => "Configuración",
        "usuarios" => "Usuarios",
        "ayuda" => "Ayuda",
        _ => CurrentSection
    };

    public string PlaceholderHint => CurrentSection switch
    {
        "clientes" => "Gestión de clientes, saldos y búsqueda por NIF — siguiente hito UI.",
        "articulos" => "Catálogo, familias y precios — conecta IProductCatalogueService en el siguiente hito.",
        "stock" => "Ajustes e inventario — Phase 4 del backend.",
        "informes" => "Informes y cuadros de mando — Phase 7.",
        "verifactu" => "Dashboard fiscal, histórico de registros y envío AEAT.",
        "caja" => "Apertura, arqueo y cierre de caja.",
        "ventas" => "Histórico de tickets y ventas del día.",
        "config" => "Preferencias de tienda, impresoras y VeriFactu.",
        "usuarios" => "Roles, capacidades y usuarios del tenant.",
        "ayuda" => "Atajos de teclado y documentación de operador.",
        _ => "Módulo en construcción."
    };

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
        if (section is "stock" && !CanAdjustStock) return;
        if (section is "informes" or "ventas" && !CanViewReports) return;
        if (section is "verifactu" && !CanManageFiscal) return;
        if (section is "usuarios" or "config" && !CanManageUsers) return;

        CurrentSection = section;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        _onLoggedOut();
    }
}
