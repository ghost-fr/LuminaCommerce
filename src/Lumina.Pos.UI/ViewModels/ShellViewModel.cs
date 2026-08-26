using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Capability-gated main shell. Visibility of nav items is driven only by
/// <see cref="IUserSession.HasCapability"/> — pure, sync, safe for bindings.
/// Capability strings match <c>Lumina.Domain.Identity.Capabilities</c> (hardcoded
/// here so the UI project does not need a Domain reference).
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    // Stable capability strings (CONTRACTS.md §0 — safe to hardcode in UI).
    public const string CapPosOperateRegister = "pos.operate_register";
    public const string CapAdminViewReports = "admin.view_reports";
    public const string CapAdminManageCatalogue = "admin.manage_catalogue";
    public const string CapStockAdjust = "stock.adjust";
    public const string CapFiscalManageVeriFactu = "fiscal.manage_verifactu";

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

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public ShellViewModel(IAuthService auth, Action onLoggedOut)
    {
        _auth = auth;
        _onLoggedOut = onLoggedOut;
        Session = auth.CurrentSession
            ?? throw new InvalidOperationException("Shell requires an authenticated session.");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        _onLoggedOut();
    }

    [RelayCommand]
    private void OpenPos()
    {
        if (!CanOperatePos) return;
        StatusMessage = "POS cart screen — next UI milestone.";
    }

    [RelayCommand]
    private void OpenCatalogue()
    {
        if (!CanManageCatalogue) return;
        StatusMessage = "Catalogue management — later phase.";
    }

    [RelayCommand]
    private void OpenStock()
    {
        if (!CanAdjustStock) return;
        StatusMessage = "Stock adjustments — Phase 4.";
    }

    [RelayCommand]
    private void OpenReports()
    {
        if (!CanViewReports) return;
        StatusMessage = "Reports — Phase 7.";
    }

    [RelayCommand]
    private void OpenFiscal()
    {
        if (!CanManageFiscal) return;
        StatusMessage = "VeriFactu management — later phase.";
    }
}
