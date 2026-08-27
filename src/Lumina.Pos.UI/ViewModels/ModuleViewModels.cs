using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Pos.UI.Mock;

namespace Lumina.Pos.UI.ViewModels;

// —— POS cart (UI mock; real CompleteSale goes through IPosSaleService later) ——

public partial class PosModuleViewModel : ObservableObject
{
    public ObservableCollection<CartLineRow> Lines { get; } = new();

    [ObservableProperty] private string _barcodeInput = string.Empty;
    [ObservableProperty] private string _statusMessage = "Listo para escanear";
    [ObservableProperty] private string _customerLabel = "CLIENTE CONTADO";

    public decimal Subtotal => Lines.Sum(l => l.LineTotal);
    public decimal VatEstimate => Math.Round(Subtotal * 0.10m, 2);
    public decimal Total => Subtotal;

    partial void OnBarcodeInputChanged(string value) { }

    [RelayCommand]
    private void AddByBarcode()
    {
        var code = BarcodeInput.Trim();
        if (string.IsNullOrEmpty(code)) return;

        var product = MockData.Products.FirstOrDefault(p =>
            p.Code.Equals(code, StringComparison.OrdinalIgnoreCase)
            || p.Name.Contains(code, StringComparison.OrdinalIgnoreCase));

        if (product is null)
        {
            StatusMessage = $"Producto no encontrado: {code}";
            return;
        }

        var existing = Lines.FirstOrDefault(l => l.Code == product.Code);
        if (existing is not null)
        {
            var idx = Lines.IndexOf(existing);
            var qty = existing.Qty + 1;
            Lines[idx] = existing with { Qty = qty, LineTotal = Math.Round(qty * existing.Price * (1 - existing.DiscountPct / 100m), 2) };
        }
        else
        {
            Lines.Add(new CartLineRow(product.Code, product.Name, 1, product.Price, 0, product.Price));
        }

        BarcodeInput = string.Empty;
        StatusMessage = $"Añadido: {product.Name}";
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(VatEstimate));
        OnPropertyChanged(nameof(Total));
    }

    [RelayCommand]
    private void KeyDigit(string digit)
    {
        BarcodeInput += digit;
    }

    [RelayCommand]
    private void KeyClear() => BarcodeInput = string.Empty;

    [RelayCommand]
    private void RemoveLast()
    {
        if (Lines.Count == 0) return;
        Lines.RemoveAt(Lines.Count - 1);
        StatusMessage = "Línea eliminada";
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(VatEstimate));
        OnPropertyChanged(nameof(Total));
    }

    [RelayCommand]
    private void ClearCart()
    {
        Lines.Clear();
        StatusMessage = "Ticket cancelado";
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(VatEstimate));
        OnPropertyChanged(nameof(Total));
    }

    [RelayCommand]
    private void CompleteMockSale()
    {
        if (Lines.Count == 0)
        {
            StatusMessage = "No hay líneas en el ticket";
            return;
        }

        StatusMessage = $"Venta simulada {Total:0.00} € — conectar IPosSaleService para fiscal real";
        Lines.Clear();
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(VatEstimate));
        OnPropertyChanged(nameof(Total));
    }
}

// —— Catalogue ——

public partial class ProductsModuleViewModel : ObservableObject
{
    private readonly List<ProductRow> _all = MockData.Products.ToList();

    public ObservableCollection<ProductRow> Items { get; } = new(MockData.Products);

    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private ProductRow? _selected;

    partial void OnSearchChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Items.Clear();
        var q = Search?.Trim() ?? string.Empty;
        foreach (var p in _all.Where(p =>
                     string.IsNullOrEmpty(q)
                     || p.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || p.Family.Contains(q, StringComparison.OrdinalIgnoreCase)))
            Items.Add(p);
    }

    [RelayCommand]
    private void Refresh() => ApplyFilter();
}

// —— Customers ——

public partial class CustomersModuleViewModel : ObservableObject
{
    private readonly List<CustomerRow> _all = MockData.Customers.ToList();
    public ObservableCollection<CustomerRow> Items { get; } = new(MockData.Customers);

    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private CustomerRow? _selected;

    partial void OnSearchChanged(string value)
    {
        Items.Clear();
        var q = Search?.Trim() ?? string.Empty;
        foreach (var c in _all.Where(c =>
                     string.IsNullOrEmpty(q)
                     || c.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || c.Nif.Contains(q, StringComparison.OrdinalIgnoreCase)))
            Items.Add(c);
    }
}

// —— Sales / tickets ——

public partial class SalesModuleViewModel : ObservableObject
{
    public ObservableCollection<TicketRow> Items { get; } = new(MockData.Tickets);
    [ObservableProperty] private TicketRow? _selected;
    [ObservableProperty] private string _filterStatus = "Todos";
}

// —— Stock ——

public partial class StockModuleViewModel : ObservableObject
{
    public ObservableCollection<ProductRow> Levels { get; } = new(MockData.Products);
    public ObservableCollection<StockMovementRow> Movements { get; } = new(MockData.StockMovements);
    [ObservableProperty] private int _selectedTab;
}

// —— Purchases & suppliers ——

public partial class PurchasesModuleViewModel : ObservableObject
{
    public ObservableCollection<PurchaseOrderRow> Orders { get; } = new(MockData.PurchaseOrders);
    public ObservableCollection<SupplierRow> Suppliers { get; } = new(MockData.Suppliers);
    [ObservableProperty] private int _selectedTab;
}

// —— Cash ——

public partial class CashModuleViewModel : ObservableObject
{
    [ObservableProperty] private decimal _openingFloat = 150m;
    [ObservableProperty] private decimal _cashSales = 420.50m;
    [ObservableProperty] private decimal _cardSales = 890.25m;
    [ObservableProperty] private decimal _cashInDrawer = 570.50m;
    [ObservableProperty] private string _status = "Caja abierta · Register 1";

    public decimal ExpectedCash => OpeningFloat + CashSales;

    [RelayCommand]
    private void CloseRegister()
    {
        Status = $"Cierre simulado — esperado {ExpectedCash:0.00} € · en cajón {CashInDrawer:0.00} €";
    }
}

// —— Reports ——

public partial class ReportsModuleViewModel : ObservableObject
{
    [ObservableProperty] private string _period = "Hoy";
    public decimal SalesTotal => 7944.20m;
    public int TicketCount => 124;
    public decimal AvgTicket => 64.07m;
    public int CustomerCount => 56;
}

// —— Users ——

public partial class UsersModuleViewModel : ObservableObject
{
    public ObservableCollection<UserRow> Items { get; } = new(MockData.Users);
    [ObservableProperty] private UserRow? _selected;
}

// —— Settings ——

public partial class SettingsModuleViewModel : ObservableObject
{
    [ObservableProperty] private string _storeName = "Main Store";
    [ObservableProperty] private string _nif = "B12345678";
    [ObservableProperty] private string _sifBoundary = "PerStore";
    [ObservableProperty] private bool _veriFactuMode = true;
    [ObservableProperty] private string _ticketPrinter = "POS-80mm";
    [ObservableProperty] private string _statusMessage = string.Empty;

    [RelayCommand]
    private void Save()
    {
        StatusMessage = "Preferencias guardadas localmente (mock UI).";
    }
}

// —— VeriFactu ——

public partial class VeriFactuModuleViewModel : ObservableObject
{
    public ObservableCollection<VeriFactuRow> Records { get; } = new(MockData.VeriFactuRecords);
    [ObservableProperty] private VeriFactuRow? _selected;
    public int Emitted => 124;
    public int Accepted => 121;
    public int Pending => 3;
    public int Rejected => 0;
    public string ConnectionStatus => "Simulado · conectar AEAT en Fiscal";
}
