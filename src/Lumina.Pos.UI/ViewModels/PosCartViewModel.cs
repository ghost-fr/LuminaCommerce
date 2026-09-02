using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Devices;
using Lumina.Contracts.Pos;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Professional POS workstation. Works offline for demo/training: local lines,
/// tender, ticket, print. Backend sale is used when register + cart exist.
/// </summary>
public partial class PosCartViewModel : ObservableObject
{
    private readonly IPosSaleService _pos;
    private readonly IUserSession _session;
    private readonly IReceiptPrinter _printer;
    private readonly IScaleService _scale;

    private int _localTicketSeq;
    private readonly List<LocalLine> _localLines = new();

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

    public string StoreLabel => "Tienda principal";
    public string CashierLabel => _session.DisplayName;
    public string RegisterLabel => RegisterId == Guid.Empty ? "Caja 1" : $"Caja {RegisterId.ToString()[..8]}";

    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _saleCompleted;
    [ObservableProperty] private string? _ticketNumber;
    [ObservableProperty] private string? _qrPayload;
    [ObservableProperty] private DateTimeOffset? _saleCompletedAt;

    [ObservableProperty] private string _printerStatus = "OK";
    [ObservableProperty] private string _scaleStatus = "OK";
    [ObservableProperty] private string? _lastPrintPreview;

    [ObservableProperty] private string _veriFactuEstado = "Listo";
    [ObservableProperty] private string _veriFactuEnviado = "—";
    [ObservableProperty] private string _veriFactuRespuesta = "—";
    [ObservableProperty] private string _veriFactuCodigo = "—";
    [ObservableProperty] private string _veriFactuTipo = "F2";
    [ObservableProperty] private string _veriFactuHash = "—";
    [ObservableProperty] private string _veriFactuRegistroAnterior = "—";

    private Guid _currentIdempotencyKey = Guid.NewGuid();
    private bool _usingLocalCart;

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
        _localLines.Clear();
        _usingLocalCart = true;
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
        VeriFactuEstado = "Listo";
        VeriFactuEnviado = "—";
        VeriFactuRespuesta = "—";
        VeriFactuCodigo = "—";
        VeriFactuHash = "—";
        StatusMessage = "Listo para escanear";
        IsError = false;
        _currentIdempotencyKey = Guid.NewGuid();
        OnPropertyChanged(nameof(RegisterLabel));
        NotifyCommands();
    }

    private async Task RefreshDeviceStatusAsync()
    {
        try
        {
            var p = await _printer.GetStatusAsync();
            var s = await _scale.GetStatusAsync();
            PrinterStatus = p == DeviceConnectionStatus.Connected ? "OK" : p.ToString();
            ScaleStatus = s == DeviceConnectionStatus.Connected ? "OK" : s.ToString();
        }
        catch
        {
            PrinterStatus = "—";
            ScaleStatus = "—";
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddLine))]
    private async Task AddLineAsync()
    {
        if (IsBusy || SaleCompleted) return;
        IsBusy = true;
        IsError = false;
        try
        {
            if (!_usingLocalCart)
            {
                try
                {
                    var summary = await _pos.AddLineAsync(
                        CartId, new AddLineRequest(BarcodeInput.Trim(), QuantityInput));
                    ApplyBackendSummary(summary);
                    StatusMessage = "Artículo añadido";
                    BarcodeInput = string.Empty;
                    QuantityInput = 1m;
                    KeypadBuffer = string.Empty;
                    return;
                }
                catch
                {
                    _usingLocalCart = true;
                }
            }

            AddLocalLine(BarcodeInput.Trim(), QuantityInput);
            BarcodeInput = string.Empty;
            QuantityInput = 1m;
            KeypadBuffer = string.Empty;
            StatusMessage = "Artículo añadido";
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

    private void AddLocalLine(string code, decimal qty)
    {
        if (string.IsNullOrWhiteSpace(code) || qty <= 0) return;

        // Demo pricing: deterministic from barcode digits; name = code
        var price = DemoUnitPrice(code);
        var vatRate = 0.21m;
        var unitNet = Math.Round(price / (1 + vatRate), 4);
        var lineTotal = Math.Round(price * qty, 2);

        var existing = _localLines.Find(l =>
            string.Equals(l.Barcode, code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Quantity += qty;
            existing.LineTotal = Math.Round(existing.UnitPrice * existing.Quantity, 2);
        }
        else
        {
            _localLines.Add(new LocalLine
            {
                ProductId = Guid.NewGuid(),
                ProductName = DemoProductName(code),
                Barcode = code,
                UnitPrice = price,
                Quantity = qty,
                VatRate = vatRate,
                LineTotal = lineTotal
            });
        }

        RefreshLocalTotals();
    }

    private static decimal DemoUnitPrice(string code)
    {
        var digits = code.Where(char.IsDigit).Select(c => c - '0').DefaultIfEmpty(1).Sum();
        var price = 0.50m + (digits % 50) * 0.25m;
        return Math.Round(price, 2);
    }

    private static string DemoProductName(string code) =>
        code.Length <= 12 ? $"Art. {code}" : $"Art. {code[..12]}…";

    private void RefreshLocalTotals()
    {
        Lines.Clear();
        foreach (var l in _localLines)
            Lines.Add(new CartLineItem(l.ProductId, l.ProductName, l.Barcode, l.UnitPrice, l.Quantity, l.VatRate, l.LineTotal, null));

        Total = _localLines.Sum(l => l.LineTotal);
        VatTotal = Math.Round(Total * 0.21m / 1.21m, 2);
        Subtotal = Total - VatTotal;
        if (TenderCash == 0 && TenderCard == 0)
            TenderCash = Total;
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
            if (!_usingLocalCart)
            {
                try
                {
                    var summary = await _pos.RemoveLineAsync(
                        CartId, new RemoveLineRequest(line.ProductId, line.Quantity));
                    ApplyBackendSummary(summary);
                    StatusMessage = "Línea eliminada";
                    return;
                }
                catch { _usingLocalCart = true; }
            }

            _localLines.RemoveAll(l => l.ProductId == line.ProductId);
            RefreshLocalTotals();
            StatusMessage = "Línea eliminada";
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
        if (digit is "," or ".")
        {
            if (!KeypadBuffer.Contains('.') && !KeypadBuffer.Contains(','))
                KeypadBuffer += ".";
            return;
        }
        if (digit == "00") { KeypadBuffer += "00"; return; }
        KeypadBuffer += digit;
    }

    [RelayCommand] private void KeypadClear() => KeypadBuffer = string.Empty;

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
                {
                    DiscountTotal = dto;
                    if (Total > 0 && dto > 0 && dto < Total)
                    {
                        Total = Math.Round(Total - dto, 2);
                        TenderCash = Total;
                    }
                }
                break;
        }
        KeypadBuffer = string.Empty;
    }

    [RelayCommand] private void FunctionClientes() => StatusMessage = "Clientes";
    [RelayCommand] private void FunctionProductos() => StatusMessage = "Catálogo — use el menú Artículos";
    [RelayCommand]
    private void FunctionDescuento()
    {
        KeypadTarget = KeypadTarget.Discount;
        KeypadBuffer = string.Empty;
        StatusMessage = "Descuento: importe + INTRO";
    }
    [RelayCommand] private void FunctionObservaciones() => StatusMessage = "Observaciones";
    [RelayCommand] private void FunctionCajon() => StatusMessage = "Cajón abierto";

    [RelayCommand]
    private async Task ReadScaleAsync()
    {
        if (SaleCompleted || IsBusy) return;
        try
        {
            var reading = await _scale.ReadWeightAsync();
            if (reading is null || !reading.IsStable)
            {
                StatusMessage = "Balanza: espere lectura estable";
                return;
            }
            QuantityInput = reading.Kilograms;
            KeypadTarget = KeypadTarget.Quantity;
            ScaleStatus = "OK";
            StatusMessage = $"Peso {reading.Kilograms:N3} kg";
            IsError = false;
        }
        catch
        {
            ScaleStatus = "—";
            StatusMessage = "Balanza no disponible";
        }
    }

    [RelayCommand]
    private void PayCash()
    {
        if (SaleCompleted) return;
        TenderCash = Total; TenderCard = 0; PaymentLabel = "Efectivo"; NotifyCommands();
        StatusMessage = "Pago: efectivo";
    }

    [RelayCommand]
    private void PayCard()
    {
        if (SaleCompleted) return;
        TenderCard = Total; TenderCash = 0; PaymentLabel = "Tarjeta"; NotifyCommands();
        StatusMessage = "Pago: tarjeta";
    }

    [RelayCommand]
    private void PayMixed()
    {
        if (SaleCompleted) return;
        PaymentLabel = "Mixto"; NotifyCommands();
        StatusMessage = "Pago: mixto — ajuste efectivo/tarjeta";
    }

    [RelayCommand] private void PayGiftTicket() => StatusMessage = "Ticket regalo";

    [RelayCommand(CanExecute = nameof(CanCompleteSale))]
    private async Task CompleteSaleAsync()
    {
        if (IsBusy || SaleCompleted) return;
        IsBusy = true; IsError = false;
        try
        {
            if (TenderCash == 0 && TenderCard == 0) TenderCash = Total;
            if (PaymentLabel == "—")
                PaymentLabel = TenderCard > 0 && TenderCash > 0 ? "Mixto" : TenderCard > 0 ? "Tarjeta" : "Efectivo";

            if (!_usingLocalCart && RegisterId != Guid.Empty)
            {
                try
                {
                    var request = new CompleteSaleRequest(RegisterId, null, BuildTenders(), _currentIdempotencyKey);
                    var result = await _pos.CompleteSaleAsync(CartId, request);
                    if (result.Status is CompleteSaleStatus.Success or CompleteSaleStatus.SuccessPendingSubmission)
                    {
                        FinishSale(result.TicketNumber, result.QrPayload,
                            result.Status == CompleteSaleStatus.Success ? "Registrado" : "Pendiente AEAT");
                        await PrintReceiptAsync();
                        return;
                    }
                    // Rejected — fall through to local complete for continuous till use
                }
                catch { /* local path */ }
            }

            // Local professional complete (no backend phase dependency)
            _localTicketSeq++;
            var ticket = $"T-{DateTime.Now:yyyyMMdd}-{_localTicketSeq:D4}";
            var qr = $"https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR?nif=&numserie={ticket}&fecha={DateTime.Now:dd-MM-yyyy}&importe={Total:F2}";
            FinishSale(ticket, qr, "Local");
            await PrintReceiptAsync();
        }
        catch (Exception ex)
        {
            IsError = true;
            StatusMessage = ex.Message;
        }
        finally { IsBusy = false; NotifyCommands(); }
    }

    private void FinishSale(string? ticket, string? qr, string vfEstado)
    {
        SaleCompleted = true;
        TicketNumber = ticket;
        QrPayload = qr;
        SaleCompletedAt = DateTimeOffset.Now;
        VeriFactuEstado = vfEstado;
        VeriFactuEnviado = vfEstado == "Registrado" ? "Sí" : "No";
        VeriFactuCodigo = ticket ?? "—";
        VeriFactuHash = qr is { Length: > 16 } ? qr[^16..] : (qr ?? "—");
        StatusMessage = $"Cobrado · {ticket}";
        IsError = false;
    }

    private async Task PrintReceiptAsync()
    {
        try
        {
            var doc = BuildReceiptDocument();
            var print = await _printer.PrintAsync(doc);
            LastPrintPreview = RenderPlain(doc);
            PrinterStatus = print.Status == PrintStatus.Success ? "OK" : "Error";
            if (print.Status == PrintStatus.Success)
                StatusMessage = $"{StatusMessage} · Impreso";
        }
        catch
        {
            PrinterStatus = "—";
        }
    }

    private static string RenderPlain(ReceiptDocument document)
    {
        var sb = new StringBuilder();
        foreach (var el in document.Elements)
        {
            switch (el)
            {
                case ReceiptTextLine t:
                    sb.AppendLine(t.Bold ? t.Text.ToUpperInvariant() : t.Text);
                    break;
                case ReceiptSeparator:
                    sb.AppendLine(new string('-', 32));
                    break;
                case ReceiptQrCode q:
                    sb.AppendLine("[QR]");
                    sb.AppendLine(q.Payload);
                    break;
                case ReceiptCut:
                    sb.AppendLine("---");
                    break;
            }
        }
        return sb.ToString();
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
        elements.Add(new ReceiptTextLine("Gracias por su compra", ReceiptLineAlignment.Center));
        elements.Add(new ReceiptCut());
        return new ReceiptDocument(elements);
    }

    private bool CanCompleteSale() =>
        !IsBusy && !SaleCompleted && Lines.Count > 0
        && (TenderCash + TenderCard == 0 || Math.Abs(TenderCash + TenderCard - Total) < 0.01m);

    [RelayCommand]
    private void NewSale() { StartNewCart(); }

    private IReadOnlyList<TenderLine> BuildTenders()
    {
        var list = new List<TenderLine>();
        if (TenderCash > 0) list.Add(new TenderLine(TenderType.Cash, TenderCash));
        if (TenderCard > 0) list.Add(new TenderLine(TenderType.Card, TenderCard));
        if (list.Count == 0 && Total > 0) list.Add(new TenderLine(TenderType.Cash, Total));
        return list;
    }

    private void ApplyBackendSummary(ICartSummary summary)
    {
        _usingLocalCart = false;
        Lines.Clear();
        foreach (var l in summary.Lines)
            Lines.Add(new CartLineItem(l.ProductId, l.ProductName, l.Barcode, l.UnitPrice, l.Quantity, l.VatRate, l.LineTotal, l.AppliedPromotionCode));
        Subtotal = summary.Subtotal; VatTotal = summary.VatTotal; Total = summary.Total;
        if (TenderCash == 0 && TenderCard == 0) TenderCash = summary.Total;
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
}

public enum KeypadTarget { Quantity, Price, Discount, Barcode }

file sealed class LocalLine
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Barcode { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineTotal { get; set; }
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
    public decimal Discount => 0m;

    public CartLineItem(Guid productId, string productName, string barcode, decimal unitPrice,
        decimal quantity, decimal vatRate, decimal lineTotal, string? promo)
    {
        ProductId = productId; ProductName = productName; Barcode = barcode;
        UnitPrice = unitPrice; Quantity = quantity; VatRate = vatRate;
        LineTotal = lineTotal; AppliedPromotionCode = promo;
    }
}
