using Lumina.Contracts.Devices;

namespace Lumina.Application.Devices;

/// <summary>
/// EMULATED receipt printer — no real hardware involved. "Prints" by
/// rendering the document to a plain-text representation and holding it in
/// memory (useful for tests and a future debug "reprint last N" feature).
/// Registered by default in DI (see DependencyInjection.cs); swap the
/// registration for a real ESC/POS adapter when one exists — nothing calling
/// IReceiptPrinter needs to know or care which is active.
/// </summary>
public sealed class SimulatedReceiptPrinter : IReceiptPrinter
{
    private readonly List<ReceiptDocument> _printed = new();

    /// <summary>Test/debug hook — every document "printed" so far, in order.
    /// A real adapter has no equivalent of this; it's specific to the
    /// simulation.</summary>
    public IReadOnlyList<ReceiptDocument> PrintedReceipts => _printed;

    public Task<DeviceConnectionStatus> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(DeviceConnectionStatus.Connected);

    public Task<PrintResult> PrintAsync(ReceiptDocument document, CancellationToken ct = default)
    {
        _printed.Add(document);
        return Task.FromResult(new PrintResult(PrintStatus.Success, null));
    }

    /// <summary>Renders a document as plain text — approximates what a real
    /// thermal printer would produce, useful for debugging/logging/tests
    /// without needing an actual printer. NOT a substitute for real ESC/POS
    /// formatting (column widths, font sizing, actual QR rendering) — a real
    /// adapter's output would look different.</summary>
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
