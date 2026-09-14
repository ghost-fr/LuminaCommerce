using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Pos.UI.Services;

namespace Lumina.Pos.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private LocalAppSettings _settings;

    [ObservableProperty] private string _storeName = "";
    [ObservableProperty] private string _connectionString = "";
    [ObservableProperty] private string _tenantId = "";
    [ObservableProperty] private bool _fiscalEnabled;
    [ObservableProperty] private string _currencySymbol = "€";
    [ObservableProperty] private decimal _defaultVatRate = 0.21m;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _settingsPath = "";

    public SettingsViewModel()
    {
        _settings = LocalSettingsStore.Load();
        SettingsPath = LocalSettingsStore.FilePath;
        LoadFromModel();
    }

    private void LoadFromModel()
    {
        StoreName = _settings.StoreName;
        ConnectionString = _settings.ConnectionString;
        TenantId = _settings.TenantId ?? "";
        FiscalEnabled = _settings.FiscalEnabled;
        CurrencySymbol = _settings.CurrencySymbol;
        DefaultVatRate = _settings.DefaultVatRate;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.StoreName = StoreName.Trim().Length > 0 ? StoreName.Trim() : "Tienda local";
        _settings.ConnectionString = string.IsNullOrWhiteSpace(ConnectionString)
            ? "Data Source=lumina.dev.db" : ConnectionString.Trim();
        _settings.TenantId = string.IsNullOrWhiteSpace(TenantId) ? null : TenantId.Trim();
        _settings.FiscalEnabled = FiscalEnabled;
        _settings.CurrencySymbol = string.IsNullOrWhiteSpace(CurrencySymbol) ? "€" : CurrencySymbol.Trim();
        _settings.DefaultVatRate = DefaultVatRate <= 0 ? 0.21m : DefaultVatRate;
        LocalSettingsStore.Save(_settings);
        StatusMessage = $"Guardado · {DateTime.Now:HH:mm:ss} · reinicie si cambió BD o tenant";
    }

    [RelayCommand]
    private void Reload()
    {
        _settings = LocalSettingsStore.Load();
        LoadFromModel();
        StatusMessage = "Recargado desde disco";
    }
}
