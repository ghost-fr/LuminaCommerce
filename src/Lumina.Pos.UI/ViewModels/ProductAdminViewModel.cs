using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Pos.UI.Services;

namespace Lumina.Pos.UI.ViewModels;

public partial class ProductAdminViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<LocalProductDto> _products = new();
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private decimal _price = 1m;
    [ObservableProperty] private decimal _vatRate = 0.21m;
    [ObservableProperty] private string _barcode = "";
    [ObservableProperty] private string _family = "General";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private LocalProductDto? _selected;

    public ProductAdminViewModel() => Reload();

    [RelayCommand]
    private void Reload()
    {
        var s = LocalSettingsStore.Load();
        Products = new ObservableCollection<LocalProductDto>(s.Products);
        StatusMessage = $"{Products.Count} productos locales";
    }

    [RelayCommand]
    private void AddProduct()
    {
        if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = "Nombre obligatorio"; return; }
        if (Price < 0) { StatusMessage = "Precio inválido"; return; }

        var code = string.IsNullOrWhiteSpace(Code) ? $"SKU-{DateTime.Now:HHmmss}" : Code.Trim();
        var barcode = string.IsNullOrWhiteSpace(Barcode) ? code : Barcode.Trim();

        var s = LocalSettingsStore.Load();
        if (s.Products.Any(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Barcode, barcode, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "Ya existe código o barcode";
            return;
        }

        var item = new LocalProductDto
        {
            Code = code,
            Name = Name.Trim(),
            Price = Math.Round(Price, 2),
            VatRate = VatRate <= 0 ? 0.21m : VatRate,
            Barcode = barcode,
            Family = string.IsNullOrWhiteSpace(Family) ? "General" : Family.Trim()
        };
        s.Products.Add(item);
        LocalSettingsStore.Save(s);
        Products.Add(item);
        StatusMessage = $"Añadido: {item.Name} · {item.Price:N2} €";
        Code = Name = Barcode = "";
        Price = 1m;
        Family = "General";
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (Selected is null) return;
        var s = LocalSettingsStore.Load();
        s.Products.RemoveAll(p => p.Code == Selected.Code);
        LocalSettingsStore.Save(s);
        Products.Remove(Selected);
        Selected = null;
        StatusMessage = "Producto eliminado";
    }
}
