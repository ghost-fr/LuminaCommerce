using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Stock;
using Lumina.Domain.Identity;

namespace Lumina.Pos.UI.ViewModels;

public partial class StockRowViewModel : ObservableObject
{
    public Guid ProductId { get; }
    public string Barcode { get; }
    public string ProductName { get; }
    public Guid StoreId { get; }
    public decimal OnHand { get; }
    public decimal Reserved { get; }
    public decimal Available { get; }

    public StockRowViewModel(IStockBalance b)
    {
        ProductId = b.ProductId;
        Barcode = b.Barcode;
        ProductName = b.ProductName;
        StoreId = b.StoreId;
        OnHand = b.OnHand;
        Reserved = b.Reserved;
        Available = b.Available;
    }
}

public partial class StockViewModel : ObservableObject
{
    private readonly IStockService _stock;
    private readonly IUserSession _session;

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    // Adjust form
    [ObservableProperty] private string _adjustBarcode = string.Empty;
    [ObservableProperty] private string _adjustDeltaText = "0";
    [ObservableProperty] private string _adjustNote = string.Empty;

    // Count form
    [ObservableProperty] private string _countBarcode = string.Empty;
    [ObservableProperty] private string _countQtyText = "0";
    [ObservableProperty] private string _countNote = string.Empty;

    public ObservableCollection<StockRowViewModel> Balances { get; } = new();

    public bool CanAdjust => _session.HasCapability(Capabilities.StockAdjust);

    public string BackendBanner =>
        "Phase 4 ledger is not implemented yet — balances are empty and adjustments are rejected until backend swaps StubStockService.";

    public StockViewModel(IStockService stock, IUserSession session)
    {
        _stock = stock;
        _session = session;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await _stock.GetBalancesAsync(new StockBalanceQuery(
                StoreId: _session.StoreId,
                BarcodeOrName: string.IsNullOrWhiteSpace(FilterText) ? null : FilterText.Trim()));

            Balances.Clear();
            foreach (var b in result.Items)
                Balances.Add(new StockRowViewModel(b));

            StatusMessage = result.TotalCount == 0
                ? "No balances (ledger pending or empty)."
                : $"{result.TotalCount} balance row(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AdjustAsync()
    {
        if (!CanAdjust)
        {
            StatusMessage = "Missing capability stock.adjust";
            return;
        }

        if (!decimal.TryParse(AdjustDeltaText, out var delta) || delta == 0)
        {
            StatusMessage = "Enter a non-zero quantity delta.";
            return;
        }

        // Resolve product via balances list or barcode-only adjust needs ProductId from backend later.
        var row = Balances.FirstOrDefault(b =>
            string.Equals(b.Barcode, AdjustBarcode.Trim(), StringComparison.OrdinalIgnoreCase));

        if (row is null)
        {
            StatusMessage = "Product not in balance list. Wait for Phase 4 ledger / seed stock, or use a listed barcode.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _stock.AdjustAsync(new AdjustStockRequest(
                row.ProductId,
                _session.StoreId,
                delta,
                string.IsNullOrWhiteSpace(AdjustNote) ? "Manual adjust" : AdjustNote.Trim(),
                Guid.NewGuid()));

            StatusMessage = result.Status == StockCommandStatus.Success
                ? $"Adjusted {row.Barcode}: on-hand {result.Balance?.OnHand}"
                : $"Rejected: {result.RejectionReason ?? result.RejectionCode.ToString()}";

            if (result.Status == StockCommandStatus.Success)
                await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CountAsync()
    {
        if (!CanAdjust)
        {
            StatusMessage = "Missing capability stock.adjust";
            return;
        }

        if (!decimal.TryParse(CountQtyText, out var qty) || qty < 0)
        {
            StatusMessage = "Enter a non-negative counted quantity.";
            return;
        }

        var row = Balances.FirstOrDefault(b =>
            string.Equals(b.Barcode, CountBarcode.Trim(), StringComparison.OrdinalIgnoreCase));

        if (row is null)
        {
            StatusMessage = "Product not in balance list. Ledger must list the SKU first.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _stock.CountAsync(new CountStockRequest(
                row.ProductId,
                _session.StoreId,
                qty,
                string.IsNullOrWhiteSpace(CountNote) ? "Physical count" : CountNote.Trim(),
                Guid.NewGuid()));

            StatusMessage = result.Status == StockCommandStatus.Success
                ? $"Count set for {row.Barcode}: on-hand {result.Balance?.OnHand}"
                : $"Rejected: {result.RejectionReason ?? result.RejectionCode.ToString()}";

            if (result.Status == StockCommandStatus.Success)
                await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
