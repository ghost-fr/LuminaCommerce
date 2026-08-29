using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Pos;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// POS cart + tender + complete-sale. Money amounts only from backend summaries.
/// UX: keyboard-first, irreversible Complete Sale distinct, rejections with next action.
/// </summary>
public partial class PosCartViewModel : ObservableObject
{
    private readonly IPosSaleService _pos;
    private readonly IUserSession _session;

    [ObservableProperty] private Guid _cartId;
    [ObservableProperty] private Guid _registerId;

    [ObservableProperty] private string _barcodeInput = string.Empty;
    [ObservableProperty] private decimal _quantityInput = 1m;

    [ObservableProperty] private ObservableCollection<CartLineItem> _lines = new();
    [ObservableProperty] private decimal _subtotal;
    [ObservableProperty] private decimal _vatTotal;
    [ObservableProperty] private decimal _total;

    [ObservableProperty] private decimal _tenderCash;
    [ObservableProperty] private decimal _tenderCard;

    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _statusHint;
    [ObservableProperty] private bool _isError;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private bool _saleCompleted;
    [ObservableProperty] private string? _saleStatusLabel;
    [ObservableProperty] private string? _ticketNumber;
    [ObservableProperty] private string? _qrPayload;

    private Guid _currentIdempotencyKey = Guid.NewGuid();

    public PosCartViewModel(IPosSaleService pos, IUserSession session, Guid? registerId = null)
    {
        _pos = pos;
        _session = session;
        RegisterId = registerId ?? Guid.Empty;
        StartNewCart();
    }

    public void StartNewCart()
    {
        CartId = Guid.NewGuid();
        Lines.Clear();
        Subtotal = VatTotal = Total = 0m;
        TenderCash = TenderCard = 0m;
        SaleCompleted = false;
        SaleStatusLabel = TicketNumber = QrPayload = null;
        StatusMessage = "Ready — scan barcode or type SKU (Enter to add).";
        StatusHint = "F2 focus scan · F8 complete sale · F9 new sale";
        IsError = false;
        _currentIdempotencyKey = Guid.NewGuid();
    }

    [RelayCommand(CanExecute = nameof(CanAddLine))]
    private async Task AddLineAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;
        StatusHint = null;
        IsError = false;

        try
        {
            var summary = await _pos.AddLineAsync(
                CartId,
                new AddLineRequest(BarcodeInput.Trim(), QuantityInput));

            ApplySummary(summary);
            BarcodeInput = string.Empty;
            QuantityInput = 1m;
            StatusMessage = "Line added.";
            StatusHint = "Continue scanning, or F8 when ready to charge.";
        }
        catch (Exception ex)
        {
            IsError = true;
            StatusMessage = "Product not found or cart not ready.";
            StatusHint = "Check barcode · ensure cart is persisted (CreateCart gap) · try again.";
            if (!string.IsNullOrWhiteSpace(ex.Message))
                StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    private bool CanAddLine() =>
        !IsBusy && !SaleCompleted && !string.IsNullOrWhiteSpace(BarcodeInput) && QuantityInput > 0;

    [RelayCommand]
    private async Task RemoveLineAsync(CartLineItem? line)
    {
        if (line is null || IsBusy || SaleCompleted) return;
        IsBusy = true;
        try
        {
            var summary = await _pos.RemoveLineAsync(
                CartId,
                new RemoveLineRequest(line.ProductId, line.Quantity));
            ApplySummary(summary);
            StatusMessage = "Line removed.";
            StatusHint = null;
            IsError = false;
        }
        catch (Exception ex)
        {
            IsError = true;
            StatusMessage = ex.Message;
            StatusHint = "Retry remove, or start a new sale (F9).";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCompleteSale))]
    private async Task CompleteSaleAsync()
    {
        if (IsBusy || SaleCompleted) return;
        IsBusy = true;
        StatusMessage = null;
        StatusHint = null;
        IsError = false;

        try
        {
            var tenders = BuildTenders();
            var request = new CompleteSaleRequest(
                RegisterId,
                CustomerId: null,
                tenders,
                _currentIdempotencyKey);

            var result = await _pos.CompleteSaleAsync(CartId, request);

            switch (result.Status)
            {
                case CompleteSaleStatus.Success:
                case CompleteSaleStatus.SuccessPendingSubmission:
                    SaleCompleted = true;
                    // Opaque tokens: display exactly as backend sent (no case transforms).
                    TicketNumber = result.TicketNumber;
                    QrPayload = result.QrPayload;
                    SaleStatusLabel = result.Status == CompleteSaleStatus.SuccessPendingSubmission
                        ? "Committed — AEAT submission pending"
                        : "Accepted";
                    StatusMessage = SaleStatusLabel;
                    StatusHint = "Print/ticket from TicketNumber + QR. F9 for next customer.";
                    IsError = false;
                    break;

                case CompleteSaleStatus.Rejected:
                    IsError = true;
                    ApplyRejection(result);
                    _currentIdempotencyKey = Guid.NewGuid();
                    break;
            }
        }
        catch (Exception ex)
        {
            IsError = true;
            StatusMessage = "Sale could not be confirmed.";
            StatusHint = "Do not print a ticket. Retry Complete sale — same cart, idempotent key kept. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    private bool CanCompleteSale() =>
        !IsBusy && !SaleCompleted && Lines.Count > 0 && RegisterId != Guid.Empty && TenderTotal() == Total;

    [RelayCommand]
    private void NewSale()
    {
        StartNewCart();
        NotifyCommands();
    }

    /// <summary>Fills cash tender to exact total (keyboard-friendly exact-pay).</summary>
    [RelayCommand]
    private void ExactCash()
    {
        if (SaleCompleted) return;
        TenderCash = Total;
        TenderCard = 0;
    }

    private void ApplyRejection(CompleteSaleResult result)
    {
        switch (result.RejectionCode)
        {
            case RejectionCode.StockUnavailable:
                StatusMessage = "Insufficient stock for one or more items.";
                StatusHint = "Reduce quantity or remove the line, then try Complete sale again.";
                break;
            case RejectionCode.PaymentValidationFailed:
                StatusMessage = "Payment does not match the total.";
                StatusHint = $"Total is {Total:0.00}. Adjust cash/card so they sum exactly (or use Exact cash).";
                break;
            case RejectionCode.RegisterNotOpen:
                StatusMessage = "Register is not open.";
                StatusHint = "Open the register (backend/SeedDev), then retry — cart is kept.";
                break;
            case RejectionCode.PriceChanged:
                StatusMessage = "A price changed since the line was added.";
                StatusHint = "Review lines, remove and re-scan affected items.";
                break;
            case RejectionCode.CustomerRequired:
                StatusMessage = "A customer is required for this sale.";
                StatusHint = "Attach a customer (when UI lands), then retry.";
                break;
            case RejectionCode.DuplicateSubmission:
                StatusMessage = "This submission was already processed.";
                StatusHint = "Check ticket history or start a new sale (F9).";
                break;
            case RejectionCode.FiscalChainError:
                StatusMessage = "Fiscal record could not be generated.";
                StatusHint = "Do not print. Retry once; if it persists, escalate — sale was not committed.";
                break;
            default:
                StatusMessage = "Sale was rejected.";
                StatusHint = string.IsNullOrWhiteSpace(result.RejectionReason)
                    ? "Adjust cart or tender and retry."
                    : result.RejectionReason;
                break;
        }

        if (!string.IsNullOrWhiteSpace(result.RejectionReason)
            && result.RejectionCode is not RejectionCode.None and not RejectionCode.Unknown)
        {
            StatusHint = (StatusHint ?? "") + "\nDetail: " + result.RejectionReason;
        }
    }

    private IReadOnlyList<TenderLine> BuildTenders()
    {
        var list = new List<TenderLine>();
        if (TenderCash > 0) list.Add(new TenderLine(TenderType.Cash, TenderCash));
        if (TenderCard > 0) list.Add(new TenderLine(TenderType.Card, TenderCard));
        return list;
    }

    private decimal TenderTotal() => TenderCash + TenderCard;

    private void ApplySummary(ICartSummary summary)
    {
        // All money figures from backend only — no client-side recompute.
        Lines.Clear();
        foreach (var l in summary.Lines)
        {
            Lines.Add(new CartLineItem(
                l.ProductId, l.ProductName, l.Barcode, l.UnitPrice, l.Quantity,
                l.VatRate, l.LineTotal, l.AppliedPromotionCode));
        }
        Subtotal = summary.Subtotal;
        VatTotal = summary.VatTotal;
        Total = summary.Total;
        if (TenderCash == 0 && TenderCard == 0)
            TenderCash = summary.Total;
    }

    private void NotifyCommands()
    {
        AddLineCommand.NotifyCanExecuteChanged();
        CompleteSaleCommand.NotifyCanExecuteChanged();
    }

    partial void OnBarcodeInputChanged(string value) => AddLineCommand.NotifyCanExecuteChanged();
    partial void OnQuantityInputChanged(decimal value) => AddLineCommand.NotifyCanExecuteChanged();
    partial void OnTenderCashChanged(decimal value) => CompleteSaleCommand.NotifyCanExecuteChanged();
    partial void OnTenderCardChanged(decimal value) => CompleteSaleCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => NotifyCommands();
    partial void OnSaleCompletedChanged(bool value) => NotifyCommands();
    partial void OnRegisterIdChanged(Guid value) => CompleteSaleCommand.NotifyCanExecuteChanged();
}

public sealed class CartLineItem
{
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string Barcode { get; }
    public decimal UnitPrice { get; }
    public decimal Quantity { get; }
    public decimal VatRate { get; }
    public decimal LineTotal { get; }
    public string? AppliedPromotionCode { get; }

    public CartLineItem(
        Guid productId, string productName, string barcode, decimal unitPrice,
        decimal quantity, decimal vatRate, decimal lineTotal, string? promo)
    {
        ProductId = productId;
        ProductName = productName;
        Barcode = barcode;
        UnitPrice = unitPrice;
        Quantity = quantity;
        VatRate = vatRate;
        LineTotal = lineTotal;
        AppliedPromotionCode = promo;
    }

    public string PromoBadge => string.IsNullOrEmpty(AppliedPromotionCode) ? "" : $"[{AppliedPromotionCode}]";
}
