namespace Lumina.Contracts.Devices;

// Barcode scanners: no port — USB/BT keyboard-wedge types digits into the
// focused barcode field. See docs/PHASE8_NOTES.md.

public enum DeviceConnectionStatus
{
    Connected,
    Disconnected,
    Error
}

public enum ReceiptLineAlignment
{
    Left,
    Center,
    Right
}

public abstract record ReceiptElement;

public sealed record ReceiptTextLine(
    string Text,
    ReceiptLineAlignment Alignment = ReceiptLineAlignment.Left,
    bool Bold = false) : ReceiptElement;

public sealed record ReceiptSeparator : ReceiptElement;

public sealed record ReceiptQrCode(string Payload) : ReceiptElement;

public sealed record ReceiptCut : ReceiptElement;

public sealed record ReceiptDocument(IReadOnlyList<ReceiptElement> Elements);

public enum PrintStatus
{
    Success,
    Failed
}

public record PrintResult(PrintStatus Status, string? ErrorMessage);

public interface IReceiptPrinter
{
    Task<DeviceConnectionStatus> GetStatusAsync(CancellationToken ct = default);
    Task<PrintResult> PrintAsync(ReceiptDocument document, CancellationToken ct = default);
}

public record WeightReading(decimal Kilograms, bool IsStable);

public interface IScaleService
{
    Task<DeviceConnectionStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Null if read failed — UI must not invent a weight.</summary>
    Task<WeightReading?> ReadWeightAsync(CancellationToken ct = default);
}
