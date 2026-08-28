using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Pos;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// POS cart + tender + complete-sale screen. Depends only on <see cref="IPosSaleService"/>
/// and the current session. Never constructs application services directly.
///
/// CONTRACT GAP (backend follow-up): there is no CreateCart / open-register method on
/// IPosSaleService yet. CartId and RegisterId must be supplied by the shell once those
/// exist (or via SeedDev for local testing). Until then NewCart creates a local Guid
/// and the first AddLine will fail with a clear message if the cart was never persisted.
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
    [ObservableProperty] private bool _isError;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private bool _saleCompleted;
    [ObservableProperty] private string? _ticketNumber;
    [ObservableProperty] private string? _qrPayload;

    /// <summary>Idempotency key for the current tender submission attempt.</summary>
    private Guid _currentIdempotencyKey = Guid.NewGuid();

    public PosCartViewModel(IPosSaleService pos, IUserSession session, Guid? registerId = null)
    {
        _pos = pos;
        _session = session;
        // RegisterId: until open-register is on the contract, shell/SeedDev must pass one.
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
        TicketNumber = QrPayload = null;
        StatusMessage = "New cart. Scan or type a barcode to add items.";
        IsError = false;
        _currentIdempotencyKey = Guid.NewGuid();
    }

    [RelayCommand(CanExecute = nameof(CanAddLine))]
    private async Task AddLineAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;
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
        }
        catch (Exception ex)
        {
            // ProductNotFoundException and missing-cart both surface here.
            IsError = true;
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
            IsError = false;
        }
        catch (Exception ex)
        {
            IsError = true;
            StatusMessage = ex.Message;
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
        IsError = false;

        try
        {
            var tenders = BuildTenders();
            // Reuse the same key on retry of THIS attempt (timeout/drop).
            // Only StartNewCart() / a Rejected-then-adjust path mints a new key.
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
                    // Both treated as success for ticket printing per CONTRACTS.md §2.2
                    SaleCompleted = true;
                    TicketNumber = result.TicketNumber;
                    QrPayload = result.QrPayload;
                    StatusMessage = result.Status == CompleteSaleStatus.SuccessPendingSubmission
                        ? $"Sale committed (ticket {result.TicketNumber}). AEAT submission pending."
                        : $"Sale complete — ticket {result.TicketNumber}";
                    IsError = false;
                    break;

                case CompleteSaleStatus.Rejected:
                    // MUST NOT clear cart, MUST NOT print
                    IsError = true;
                    StatusMessage = FormatRejection(result);
                    // New attempt after operator adjusts → new idempotency key
                    _currentIdempotencyKey = Guid.NewGuid();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Timeout / unknown — treat as failed, never as success. Same key kept for retry.
            IsError = true;
            StatusMessage = $"Sale could not be confirmed: {ex.Message}. Retry with the same cart (idempotent).";
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
        // Default tender to exact total for fast cash path
        if (TenderCash == 0 && TenderCard == 0)
            TenderCash = summary.Total;
    }

    private static string FormatRejection(CompleteSaleResult result)
    {
        var primary = result.RejectionCode switch
        {
            RejectionCode.StockUnavailable => "Insufficient stock for one or more items.",
            RejectionCode.PaymentValidationFailed => "Payment amount does not match the total.",
            RejectionCode.RegisterNotOpen => "Register is not open.",
            RejectionCode.PriceChanged => "A price changed — review the cart.",
            RejectionCode.CustomerRequired => "A customer is required for this sale.",
            RejectionCode.DuplicateSubmission => "This sale was already submitted.",
            RejectionCode.FiscalChainError => "Fiscal record could not be generated.",
            _ => "Sale was rejected."
        };
        return string.IsNullOrWhiteSpace(result.RejectionReason)
            ? primary
            : $"{primary}\n{result.RejectionReason}";
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

/// <summary>UI-bindable projection of ICartLine (ObservableObject for list virtualization).</summary>
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
