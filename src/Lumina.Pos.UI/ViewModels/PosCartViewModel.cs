using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Pos;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Dense supermarket POS workstation (GesVent VisorTpv / ticket layout as structural spine).
/// Regions: ticket grid, function keys, totals/payment, numeric keypad, receipt preview, VeriFactu.
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
    [ObservableProperty] private CartLineItem? _selectedLine;

    [ObservableProperty] private decimal _subtotal;
    [ObservableProperty] private decimal _vatTotal;
    [ObservableProperty] private decimal _discountTotal;
    [ObservableProperty] private decimal _total;
    [ObservableProperty] private decimal _tenderCash;
    [ObservableProperty] private decimal _tenderCard;
    [ObservableProperty] private string _paymentLabel = "—";

    [ObservableProperty] private string _keypadBuffer = string.Empty;
    [ObservableProperty] private KeypadTarget _keypadTarget = KeypadTarget.Quantity;

    public string StoreLabel => $"Store {_session.StoreId.ToString()[..8]}…";
    public string CashierLabel => _session.DisplayName;
    public string RegisterLabel => RegisterId == Guid.Empty ? "Caja —" : $"Caja {RegisterId.ToString()[..8]}…";

    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _saleCompleted;
    [ObservableProperty] private string? _ticketNumber;
    [ObservableProperty] private string? _qrPayload;
    [ObservableProperty] private DateTimeOffset? _saleCompletedAt;

    [ObservableProperty] private string _veriFactuEstado = "—";
    [ObservableProperty] private string _veriFactuEnviado = "—";
    [ObservableProperty] private string _veriFactuRespuesta = "—";
    [ObservableProperty] private string _veriFactuCodigo = "—";
    [ObservableProperty] private string _veriFactuTipo = "F2";
    [ObservableProperty] private string _veriFactuHash = "—";
    [ObservableProperty] private string _veriFactuRegistroAnterior = "—";

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
        SelectedLine = null;
        Subtotal = VatTotal = DiscountTotal = Total = 0m;
        TenderCash = TenderCard = 0m;
        PaymentLabel = "—";
        SaleCompleted = false;
        TicketNumber = QrPayload = null;
        SaleCompletedAt = null;
        KeypadBuffer = string.Empty;
        KeypadTarget = KeypadTarget.Quantity;
        QuantityInput = 1m;
        BarcodeInput = string.Empty;
        ResetVeriFactu();
        StatusMessage = "Nuevo ticket. Escanee o teclee un código de barras.";
        IsError = false;
        _currentIdempotencyKey = Guid.NewGuid();
        OnPropertyChanged(nameof(RegisterLabel));
    }

    private void ResetVeriFactu()
    {
        VeriFactuEstado = "Pendiente";
        VeriFactuEnviado = "No";
        VeriFactuRespuesta = "—";
        VeriFactuCodigo = "—";
        VeriFactuTipo = "F2";
        VeriFactuHash = "—";
        VeriFactuRegistroAnterior = "—";
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
                CartId, new AddLineRequest(BarcodeInput.Trim(), QuantityInput));
            ApplySummary(summary);
            BarcodeInput = string.Empty;
            QuantityInput = 1m;
            KeypadBuffer = string.Empty;
            StatusMessage = "Línea añadida.";
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
                CartId, new RemoveLineRequest(line.ProductId, line.Quantity));
            ApplySummary(summary);
            StatusMessage = "Línea eliminada.";
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

    [RelayCommand]
    private void KeypadDigit(string? digit)
    {
        if (SaleCompleted || digit is null) return;
        if (digit == "," || digit == ".")
        {
            if (!KeypadBuffer.Contains('.') && !KeypadBuffer.Contains(','))
                KeypadBuffer += ".";
            return;
        }
        if (digit == "00")
        {
            KeypadBuffer += "00";
            return;
        }
        KeypadBuffer += digit;
    }

    [RelayCommand]
    private void KeypadClear() => KeypadBuffer = string.Empty;

    [RelayCommand]
    private void KeypadBackspace()
    {
        if (KeypadBuffer.Length > 0)
            KeypadBuffer = KeypadBuffer[..^1];
    }

    [RelayCommand]
    private void SetKeypadTarget(string? target)
    {
        if (Enum.TryParse<KeypadTarget>(target, ignoreCase: true, out var t))
        {
            KeypadTarget = t;
            KeypadBuffer = string.Empty;
            StatusMessage = t switch
            {
                KeypadTarget.Quantity => "Teclado → Cantidad",
                KeypadTarget.Price => "Teclado → Precio (solo visual hasta API)",
                KeypadTarget.Discount => "Teclado → Descuento (solo visual hasta API)",
                KeypadTarget.Barcode => "Teclado → Código de barras",
                _ => StatusMessage
            };
        }
    }

    [RelayCommand]
    private async Task KeypadEnterAsync()
    {
        if (SaleCompleted) return;

        switch (KeypadTarget)
        {
            case KeypadTarget.Quantity:
                if (decimal.TryParse(KeypadBuffer.Replace(',', '.'),
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var qty) && qty > 0)
                {
                    QuantityInput = qty;
                    StatusMessage = $"Cantidad = {qty:N2}";
                }
                break;

            case KeypadTarget.Barcode:
                if (!string.IsNullOrWhiteSpace(KeypadBuffer))
                {
                    BarcodeInput = KeypadBuffer;
                    await AddLineAsync();
                }
                break;

            case KeypadTarget.Discount:
                if (decimal.TryParse(KeypadBuffer.Replace(',', '.'),
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var dto))
                {
                    DiscountTotal = dto;
                    StatusMessage = $"Descuento anotado: {dto:C} (pendiente API de línea)";
                }
                break;

            case KeypadTarget.Price:
                StatusMessage = "Cambio de precio: requiere pos.override_price + API (pendiente).";
                break;
        }

        KeypadBuffer = string.Empty;
    }

    [RelayCommand]
    private void FunctionClientes() =>
        StatusMessage = "F3 Clientes — selector de cliente (pendiente módulo CRM).";

    [RelayCommand]
    private void FunctionProductos() =>
        StatusMessage = "F4 Productos — catálogo rápido (pendiente).";

    [RelayCommand]
    private void FunctionDescuento()
    {
        KeypadTarget = KeypadTarget.Discount;
        KeypadBuffer = string.Empty;
        StatusMessage = "F5 Descuento — introduzca importe en el teclado y Enter.";
    }

    [RelayCommand]
    private void FunctionObservaciones() =>
        StatusMessage = "F6 Observaciones — notas de ticket (pendiente).";

    [RelayCommand]
    private void FunctionCajon() =>
        StatusMessage = "F7 Cajón — apertura de cajón (hardware, pendiente).";

    [RelayCommand]
    private void PayCash()
    {
        if (SaleCompleted) return;
        TenderCash = Total;
        TenderCard = 0;
        PaymentLabel = "Efectivo";
        StatusMessage = "F8 Efectivo — total asignado a efectivo.";
        NotifyCommands();
    }

    [RelayCommand]
    private void PayCard()
    {
        if (SaleCompleted) return;
        TenderCard = Total;
        TenderCash = 0;
        PaymentLabel = "Tarjeta";
        StatusMessage = "F9 Tarjeta — total asignado a tarjeta.";
        NotifyCommands();
    }

    [RelayCommand]
    private void PayMixed()
    {
        if (SaleCompleted) return;
        PaymentLabel = "Mixto";
        StatusMessage = "F10 Mixto — ajuste efectivo/tarjeta en totales.";
        NotifyCommands();
    }

    [RelayCommand]
    private void PayGiftTicket() =>
        StatusMessage = "F11 Ticket regalo — pendiente de contrato fiscal.";

    [RelayCommand(CanExecute = nameof(CanCompleteSale))]
    private async Task CompleteSaleAsync()
    {
        if (IsBusy || SaleCompleted) return;
        IsBusy = true;
        StatusMessage = null;
        IsError = false;

        try
        {
            if (TenderCash == 0 && TenderCard == 0)
                TenderCash = Total;

            var tenders = BuildTenders();
            var request = new CompleteSaleRequest(
                RegisterId, CustomerId: null, tenders, _currentIdempotencyKey);

            var result = await _pos.CompleteSaleAsync(CartId, request);

            switch (result.Status)
            {
                case CompleteSaleStatus.Success:
                case CompleteSaleStatus.SuccessPendingSubmission:
                    SaleCompleted = true;
                    TicketNumber = result.TicketNumber;
                    QrPayload = result.QrPayload;
                    SaleCompletedAt = DateTimeOffset.Now;
                    if (string.IsNullOrEmpty(PaymentLabel) || PaymentLabel == "—")
                        PaymentLabel = TenderCard > 0 && TenderCash > 0 ? "Mixto"
                            : TenderCard > 0 ? "Tarjeta" : "Efectivo";

                    VeriFactuEstado = result.Status == CompleteSaleStatus.SuccessPendingSubmission
                        ? "Pendiente envío"
                        : "Aceptado";
                    VeriFactuEnviado = result.Status == CompleteSaleStatus.Success ? "Sí" : "Pendiente";
                    VeriFactuRespuesta = result.Status.ToString();
                    VeriFactuCodigo = result.TicketNumber ?? "—";
                    VeriFactuHash = result.QrPayload is { Length: > 16 }
                        ? result.QrPayload[^16..]
                        : (result.QrPayload ?? "—");

                    StatusMessage = result.Status == CompleteSaleStatus.SuccessPendingSubmission
                        ? $"Ticket {result.TicketNumber} — AEAT pendiente."
                        : $"Ticket {result.TicketNumber} cobrado.";
                    IsError = false;
                    break;

                case CompleteSaleStatus.Rejected:
                    IsError = true;
                    StatusMessage = FormatRejection(result);
                    VeriFactuEstado = "Rechazado";
                    VeriFactuRespuesta = result.RejectionCode.ToString();
                    _currentIdempotencyKey = Guid.NewGuid();
                    break;
            }
        }
        catch (Exception ex)
        {
            IsError = true;
            StatusMessage = $"Cobro no confirmado: {ex.Message}. Reintente (idempotente).";
            VeriFactuEstado = "Error";
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    private bool CanCompleteSale() =>
        !IsBusy && !SaleCompleted && Lines.Count > 0 && RegisterId != Guid.Empty
        && (TenderCash + TenderCard == 0 || TenderCash + TenderCard == Total);

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
        if (list.Count == 0 && Total > 0)
            list.Add(new TenderLine(TenderType.Cash, Total));
        return list;
    }

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
        if (TenderCash == 0 && TenderCard == 0)
            TenderCash = summary.Total;
    }

    private static string FormatRejection(CompleteSaleResult result)
    {
        var primary = result.RejectionCode switch
        {
            RejectionCode.StockUnavailable => "Stock insuficiente.",
            RejectionCode.PaymentValidationFailed => "El pago no coincide con el total.",
            RejectionCode.RegisterNotOpen => "La caja no está abierta.",
            RejectionCode.PriceChanged => "Un precio cambió — revise el ticket.",
            RejectionCode.CustomerRequired => "Se requiere cliente.",
            RejectionCode.DuplicateSubmission => "Venta ya enviada.",
            RejectionCode.FiscalChainError => "Error de cadena fiscal.",
            _ => "Venta rechazada."
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

public enum KeypadTarget { Quantity, Price, Discount, Barcode }

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
    public decimal Discount => 0m;

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
}
