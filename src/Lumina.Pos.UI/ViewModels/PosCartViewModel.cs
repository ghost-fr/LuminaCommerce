using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Devices;
using Lumina.Contracts.Pos;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Dense supermarket POS workstation. Phase 8: emulated printer + scale drivers.
/// </summary>
public partial class PosCartViewModel : ObservableObject
{
    private readonly IPosSaleService _pos;
    private readonly IUserSession _session;
    private readonly IReceiptPrinter _printer;
    private readonly IScaleService _scale;

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

    [ObservableProperty] private string _printerStatus = "—";
    [ObservableProperty] private string _scaleStatus = "—";
    [ObservableProperty] private string? _lastPrintPreview;

    [ObservableProperty] private string _veriFactuEstado = "—";
    [ObservableProperty] private string _veriFactuEnviado = "—";
    [ObservableProperty] private string _veriFactuRespuesta = "—";
    [ObservableProperty] private string _veriFactuCodigo = "—";
    [ObservableProperty] private string _veriFactuTipo = "F2";
    [ObservableProperty] private string _veriFactuHash = "—";
    [ObservableProperty] private string _veriFactuRegistroAnterior = "—";

    private Guid _currentIdempotencyKey = Guid.NewGuid();

    public PosCartViewModel(
        IPosSaleService pos,
        IUserSession session,
        IReceiptPrinter printer,
        IScaleService scale,
        Guid? registerId = null)
    {
        _pos = pos;
        _session = session;
        _printer = printer;
        _scale = scale;
        RegisterId = registerId ?? Guid.Empty;
        StartNewCart();
        _ = RefreshDeviceStatusAsync();
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
        LastPrintPreview = null;
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

    private async Task RefreshDeviceStatusAsync()
    {
        try
        {
            var p = await _printer.GetStatusAsync();
            var s = await _scale.GetStatusAsync();
            PrinterStatus = p.ToString();
            ScaleStatus = s.ToString();
        }
        catch
        {
            PrinterStatus = "Error";
            ScaleStatus = "Error";
        }
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
        if (digit == "00") { KeypadBuffer += "00"; return; }
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
                        NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) && qty > 0)
                    QuantityInput = qty;
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
                        NumberStyles.Number, CultureInfo.InvariantCulture, out var dto))
                    DiscountTotal = dto;
                break;
        }
        KeypadBuffer = string.Empty;
    }

    [RelayCommand] private void FunctionClientes() => StatusMessage = "F3 Clientes (pendiente).";
    [RelayCommand] private void FunctionProductos() => StatusMessage = "F4 Productos (pendiente).";
    [RelayCommand]
    private void FunctionDescuento()
    {
        KeypadTarget = KeypadTarget.Discount;
        KeypadBuffer = string.Empty;
        StatusMessage = "F5 Descuento — teclado + Intro.";
    }
    [RelayCommand] private void FunctionObservaciones() => StatusMessage = "F6 Observaciones (pendiente).";
    [RelayCommand] private void FunctionCajon() => StatusMessage = "F7 Cajón — sin puerto de cajón aún (solo impresora/balanza Phase 8).";

    /// <summary>Read emulated scale into Quantity for weighed products.</summary>
    [RelayCommand]
    private async Task ReadScaleAsync()
    {
        if (SaleCompleted || IsBusy) return;
        try
        {
            var reading = await _scale.ReadWeightAsync();
            if (reading is null)
            {
                IsError = true;
                StatusMessage = "Balanza: sin lectura.";
                return;
            }
            if (!reading.IsStable)
            {
                StatusMessage = $"Balanza inestable: {reading.Kilograms:N3} kg — espere.";
                return;
            }
            QuantityInput = reading.Kilograms;
            KeypadTarget = KeypadTarget.Quantity;
            ScaleStatus = "Connected";
            StatusMessage = $"Peso {reading.Kilograms:N3} kg → cantidad. Escanee artículo.";
            IsError = false;
        }
        catch (Exception ex)
        {
            IsError = true;
            ScaleStatus = "Error";
            StatusMessage = $"Balanza: {ex.Message}";
        }
    }

    [RelayCommand]
    private void PayCash()
    {
        if (SaleCompleted) return;
        TenderCash = Total; TenderCard = 0; PaymentLabel = "Efectivo"; NotifyCommands();
    }

    [RelayCommand]
    private void PayCard()
    {
        if (SaleCompleted) return;
        TenderCard = Total; TenderCash = 0; PaymentLabel = "Tarjeta"; NotifyCommands();
    }

    [RelayCommand]
    private void PayMixed()
    {
        if (SaleCompleted) return;
        PaymentLabel = "Mixto"; NotifyCommands();
    }

    [RelayCommand] private void PayGiftTicket() => StatusMessage = "F11 Ticket regalo (pendiente).";

    [RelayCommand(CanExecute = nameof(CanCompleteSale))]
    private async Task CompleteSaleAsync()
    {
        if (IsBusy || SaleCompleted) return;
        IsBusy = true; StatusMessage = null; IsError = false;
        try
        {
            if (TenderCash == 0 && TenderCard == 0) TenderCash = Total;
            var tenders = BuildTenders();
            var request = new CompleteSaleRequest(RegisterId, null, tenders, _currentIdempotencyKey);
            var result = await _pos.CompleteSaleAsync(CartId, request);
            switch (result.Status)
            {
                case CompleteSaleStatus.Success:
                case CompleteSaleStatus.SuccessPendingSubmission:
                    SaleCompleted = true;
                    TicketNumber = result.TicketNumber;
                    QrPayload = result.QrPayload;
                    SaleCompletedAt = DateTimeOffset.Now;
                    if (PaymentLabel == "—")
                        PaymentLabel = TenderCard > 0 && TenderCash > 0 ? "Mixto" : TenderCard > 0 ? "Tarjeta" : "Efectivo";
                    VeriFactuEstado = result.Status == CompleteSaleStatus.SuccessPendingSubmission ? "Pendiente envío" : "Aceptado";
                    VeriFactuEnviado = result.Status == CompleteSaleStatus.Success ? "Sí" : "Pendiente";
                    VeriFactuRespuesta = result.Status.ToString();
                    VeriFactuCodigo = result.TicketNumber ?? "—";
                    VeriFactuHash = result.QrPayload is { Length: > 16 } ? result.QrPayload[^16..] : (result.QrPayload ?? "—");
                    StatusMessage = $"Ticket {result.TicketNumber}";
                    IsError = false;
                    await PrintReceiptAsync();
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
            StatusMessage = $"Cobro no confirmado: {ex.Message}";
            VeriFactuEstado = "Error";
        }
        finally { IsBusy = false; NotifyCommands(); }
    }

    private async Task PrintReceiptAsync()
    {
        try
        {
            var doc = BuildReceiptDocument();
            var print = await _printer.PrintAsync(doc);
            PrinterStatus = (await _printer.GetStatusAsync()).ToString();
            if (print.Status == PrintStatus.Success)
            {
                LastPrintPreview = Lumina.Application.Devices.SimulatedReceiptPrinter.RenderAsPlainText(doc);
                StatusMessage = $"{StatusMessage} · Impreso (simulado).";
            }
            else
            {
                StatusMessage = $"{StatusMessage} · Impresión fallida: {print.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            PrinterStatus = "Error";
            StatusMessage = $"{StatusMessage} · Impresora: {ex.Message}";
        }
    }

    private ReceiptDocument BuildReceiptDocument()
    {
        var elements = new List<ReceiptElement>
        {
            new ReceiptTextLine(StoreLabel, ReceiptLineAlignment.Center, Bold: true),
            new ReceiptTextLine($"Cajero: {CashierLabel}", ReceiptLineAlignment.Center),
            new ReceiptSeparator(),
            new ReceiptTextLine($"Ticket: {TicketNumber ?? "—"}"),
            new ReceiptTextLine((SaleCompletedAt ?? DateTimeOffset.Now).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
            new ReceiptSeparator()
        };

        foreach (var line in Lines)
        {
            elements.Add(new ReceiptTextLine(line.ProductName));
            elements.Add(new ReceiptTextLine(
                $"{line.Quantity:N2} x {line.UnitPrice:N2} = {line.LineTotal:N2}",
                ReceiptLineAlignment.Right));
        }

        elements.Add(new ReceiptSeparator());
        elements.Add(new ReceiptTextLine($"Subtotal: {Subtotal:N2}", ReceiptLineAlignment.Right));
        elements.Add(new ReceiptTextLine($"IVA: {VatTotal:N2}", ReceiptLineAlignment.Right));
        elements.Add(new ReceiptTextLine($"TOTAL: {Total:N2}", ReceiptLineAlignment.Right, Bold: true));
        elements.Add(new ReceiptSeparator());
        if (TenderCash > 0)
            elements.Add(new ReceiptTextLine($"Efectivo: {TenderCash:N2}", ReceiptLineAlignment.Right));
        if (TenderCard > 0)
            elements.Add(new ReceiptTextLine($"Tarjeta: {TenderCard:N2}", ReceiptLineAlignment.Right));
        if (!string.IsNullOrWhiteSpace(QrPayload))
            elements.Add(new ReceiptQrCode(QrPayload));
        elements.Add(new ReceiptTextLine("Documento verificable en aeat.es", ReceiptLineAlignment.Center));
        elements.Add(new ReceiptCut());
        return new ReceiptDocument(elements);
    }

    private bool CanCompleteSale() =>
        !IsBusy && !SaleCompleted && Lines.Count > 0 && RegisterId != Guid.Empty
        && (TenderCash + TenderCard == 0 || TenderCash + TenderCard == Total);

    [RelayCommand]
    private void NewSale() { StartNewCart(); NotifyCommands(); }

    private IReadOnlyList<TenderLine> BuildTenders()
    {
        var list = new List<TenderLine>();
        if (TenderCash > 0) list.Add(new TenderLine(TenderType.Cash, TenderCash));
        if (TenderCard > 0) list.Add(new TenderLine(TenderType.Card, TenderCard));
        if (list.Count == 0 && Total > 0) list.Add(new TenderLine(TenderType.Cash, Total));
        return list;
    }

    private void ApplySummary(ICartSummary summary)
    {
        Lines.Clear();
        foreach (var l in summary.Lines)
            Lines.Add(new CartLineItem(l.ProductId, l.ProductName, l.Barcode, l.UnitPrice, l.Quantity, l.VatRate, l.LineTotal, l.AppliedPromotionCode));
        Subtotal = summary.Subtotal; VatTotal = summary.VatTotal; Total = summary.Total;
        if (TenderCash == 0 && TenderCard == 0) TenderCash = summary.Total;
    }

    private static string FormatRejection(CompleteSaleResult result)
    {
        var primary = result.RejectionCode switch
        {
            RejectionCode.StockUnavailable => "Stock insuficiente.",
            RejectionCode.PaymentValidationFailed => "Pago no coincide.",
            RejectionCode.RegisterNotOpen => "Caja no abierta.",
            RejectionCode.PriceChanged => "Precio cambió.",
            RejectionCode.CustomerRequired => "Se requiere cliente.",
            RejectionCode.DuplicateSubmission => "Venta ya enviada.",
            RejectionCode.FiscalChainError => "Error fiscal.",
            _ => "Venta rechazada."
        };
        return string.IsNullOrWhiteSpace(result.RejectionReason) ? primary : $"{primary}\n{result.RejectionReason}";
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

    public CartLineItem(Guid productId, string productName, string barcode, decimal unitPrice,
        decimal quantity, decimal vatRate, decimal lineTotal, string? promo)
    {
        ProductId = productId; ProductName = productName; Barcode = barcode;
        UnitPrice = unitPrice; Quantity = quantity; VatRate = vatRate;
        LineTotal = lineTotal; AppliedPromotionCode = promo;
    }
}
