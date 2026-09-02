using Lumina.Contracts.Devices;

namespace Lumina.Application.Devices;

/// <summary>
/// EMULATED receipt printer — holds documents in memory. Swap DI registration
/// for a real ESC/POS adapter when hardware is confirmed (PHASE8_NOTES.md).
/// </summary>
public sealed class SimulatedReceiptPrinter : IReceiptPrinter
{
    private readonly List<ReceiptDocument> _printed = new();

    public IReadOnlyList<ReceiptDocument> PrintedReceipts => _printed;

    public Task<DeviceConnectionStatus> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(DeviceConnectionStatus.Connected);

    public Task<PrintResult> PrintAsync(ReceiptDocument document, CancellationToken ct = default)
    {
        _printed.Add(document);
        return Task.FromResult(new PrintResult(PrintStatus.Success, null));
    }

    public static string RenderAsPlainText(ReceiptDocument document)
    {
        var lines = new List<string>();
        foreach (var element in document.Elements)
        {
            switch (element)
            {
                case ReceiptTextLine text:
                    var content = text.Bold ? text.Text.ToUpperInvariant() : text.Text;
                    lines.Add(text.Alignment switch
                    {
                        ReceiptLineAlignment.Center => content.PadLeft((40 + content.Length) / 2).PadRight(40),
                        ReceiptLineAlignment.Right => content.PadLeft(40),
                        _ => content
                    });
                    break;
                case ReceiptSeparator:
                    lines.Add(new string('-', 40));
                    break;
                case ReceiptQrCode qr:
                    lines.Add($"[QR: {qr.Payload}]");
                    break;
                case ReceiptCut:
                    lines.Add("--- CUT ---");
                    break;
            }
        }
        return string.Join(Environment.NewLine, lines);
    }
}
