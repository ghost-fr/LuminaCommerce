namespace Lumina.Contracts.Devices;

// ============================================================================
// Barcode scanners — DELIBERATELY NO PORT HERE.
//
// The overwhelming majority of retail barcode scanners (USB or Bluetooth) are
// "keyboard-wedge" devices: to the operating system they present as a
// keyboard, and scanning a barcode just types the digits (plus Enter) into
// whatever text field currently has focus. This already works with the
// existing IPosSaleService.AddLineAsync(cartId, AddLineRequest) contract —
// the barcode search box shown in the POS mockup — with ZERO backend
// integration needed. Building a fake IBarcodeScanner port here would
// misrepresent how this hardware actually works and add an abstraction
// nothing needs.
//
// The exception: some industrial/specialized scanners connect via raw
// serial/Bluetooth-SPP and need explicit driver code to read scan events.
// If Lumina ever needs to support one of those, that's a real, separate
// integration to build against the SPECIFIC device's documented protocol —
// not something to guess at generically here. Flagged in PHASE8_NOTES.md.
// ============================================================================

public enum DeviceConnectionStatus
{
    Connected,
    Disconnected,
    Error
}

// ----------------------------------------------------------------------------
// Receipt printer — a small, printer-appropriate document model, distinct
// from the on-screen ticket preview (that's a UI/XAML concern, already shown
// working in the POS mockup). Thermal receipt printers (the common case for
// Spanish SMB retail) don't use the OS print dialog — they take raw
// text/command sequences over a port (commonly ESC/POS). This model is
// designed to be directly translatable to that, without committing to any
// specific printer brand's exact command set.
// ----------------------------------------------------------------------------

public enum ReceiptLineAlignment
{
    Left,
    Center,
    Right
}

public abstract record ReceiptElement;

public sealed record ReceiptTextLine(string Text, ReceiptLineAlignment Alignment = ReceiptLineAlignment.Left, bool Bold = false) : ReceiptElement;
public sealed record ReceiptSeparator : ReceiptElement;
public sealed record ReceiptQrCode(string Payload) : ReceiptElement;

/// <summary>Signals end of receipt content — a real ESC/POS adapter maps this
/// to the paper-cut command. Always the last element in a well-formed document,
/// though nothing currently enforces that; flagged as worth validating if this
/// ever feeds a real printer adapter.</summary>
public sealed record ReceiptCut : ReceiptElement;

public sealed record ReceiptDocument(IReadOnlyList<ReceiptElement> Elements);

public enum PrintStatus
{
    Success,
    Failed
}

public record PrintResult(PrintStatus Status, string? ErrorMessage);

/// <summary>
/// UI-facing contract for printing a receipt. Concrete implementation
/// (currently EMULATED — see PHASE8_NOTES.md) is
/// Lumina.Application.Devices.SimulatedReceiptPrinter, wired in DI. A real
/// hardware adapter (ESC/POS over USB/network) would implement this same
/// interface and swap the DI registration — nothing above this port should
/// need to change when that happens.
/// </summary>
public interface IReceiptPrinter
{
    Task<DeviceConnectionStatus> GetStatusAsync(CancellationToken ct = default);
    Task<PrintResult> PrintAsync(ReceiptDocument document, CancellationToken ct = default);
}

// ----------------------------------------------------------------------------
// Scale — for weighed products (produce, deli, etc.). Real scale protocols
// (serial/RS232 or USB-serial) vary significantly by manufacturer (Toledo,
// CAS, Dibal, Mettler Toledo, and others all differ) — building a real driver
// requires the SPECIFIC device's documented protocol, not a guess. See
// PHASE8_NOTES.md.
// ----------------------------------------------------------------------------

/// <summary>IsStable indicates the scale's reading has settled (not still
/// fluctuating as an item is placed on it) — real scales expose this; UI
/// should wait for IsStable before accepting a reading into a sale.</summary>
public record WeightReading(decimal Kilograms, bool IsStable);

/// <summary>
/// UI-facing contract for reading a scale. Concrete implementation (currently
/// EMULATED) is Lumina.Application.Devices.SimulatedScaleService.
///
/// Integration point: after reading a weight, UI calls the EXISTING
/// IPosSaleService.AddLineAsync(cartId, new AddLineRequest(barcode, reading.
/// Kilograms)) — Quantity is already decimal, so no contract change was
/// needed to support fractional (weighed) quantities.
/// </summary>
public interface IScaleService
{
    Task<DeviceConnectionStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Null if the read failed or timed out (e.g. nothing on the
    /// scale, device disconnected) — UI shows a retry state, never fabricates
    /// a zero/placeholder weight.</summary>
    Task<WeightReading?> ReadWeightAsync(CancellationToken ct = default);
}
