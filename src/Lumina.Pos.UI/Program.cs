using System.Diagnostics;
using Avalonia;

namespace Lumina.Pos.UI;

class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // WinExe has no console by default — attach one so exceptions are visible.
        // Also write pos-crash.log next to the exe for silent exits.
        try
        {
            AttachConsoleIfPossible();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            var text = ex.ToString();
            Console.Error.WriteLine(text);
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "pos-crash.log");
                File.WriteAllText(path, $"{DateTimeOffset.Now:o}\n{text}\n");
                Console.Error.WriteLine($"Wrote crash log: {path}");
            }
            catch { /* ignore secondary failures */ }

            if (Debugger.IsAttached == false && Environment.UserInteractive)
            {
                Console.Error.WriteLine("Press Enter to exit...");
                try { Console.ReadLine(); } catch { }
            }
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static void AttachConsoleIfPossible()
    {
        try
        {
            AllocConsole();
        }
        catch
        {
            // Not on Windows or already has a console — fine.
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
}
