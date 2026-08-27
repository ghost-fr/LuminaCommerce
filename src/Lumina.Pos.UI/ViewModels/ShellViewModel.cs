using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Capability-gated main shell matching Figma nav structure.
/// Section visibility uses <see cref="IUserSession.HasCapability"/> only.
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

    public bool CanOperatePos => Session.HasCapability(CapPosOperateRegister);
    public bool CanViewReports => Session.HasCapability(CapAdminViewReports);
    public bool CanManageCatalogue => Session.HasCapability(CapAdminManageCatalogue);
    public bool CanAdjustStock => Session.HasCapability(CapStockAdjust);
    public bool CanManageFiscal => Session.HasCapability(CapFiscalManageVeriFactu);
    public bool CanManageUsers => Session.HasCapability(CapAdminManageUsers);

    /// <summary>inicio | tpv | ventas | clientes | articulos | stock | informes | verifactu | caja | config | usuarios | ayuda</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInicio))]
    [NotifyPropertyChangedFor(nameof(IsTpv))]
    [NotifyPropertyChangedFor(nameof(IsPlaceholder))]
    [NotifyPropertyChangedFor(nameof(PlaceholderTitle))]
    private string _currentSection = "inicio";

    public bool IsInicio => CurrentSection == "inicio";
    public bool IsTpv => CurrentSection == "tpv";
    public bool IsPlaceholder => !IsInicio && !IsTpv;

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

    [ObservableProperty]
    private string _statusMessage = "Listo";

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

        // Capability gates for restricted sections
        if (section is "tpv" or "caja" && !CanOperatePos) return;
        if (section is "articulos" && !CanManageCatalogue) return;
        if (section is "stock" && !CanAdjustStock) return;
        if (section is "informes" or "ventas" && !CanViewReports) return;
        if (section is "verifactu" && !CanManageFiscal) return;
        if (section is "usuarios" or "config" && !CanManageUsers) return;

        CurrentSection = section;
        StatusMessage = section switch
        {
            "inicio" => "Panel de inicio",
            "tpv" => "TPV — pantalla de venta",
            _ => $"{PlaceholderTitle} — próximamente"
        };
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        _onLoggedOut();
    }
}
