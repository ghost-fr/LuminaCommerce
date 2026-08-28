using System.Diagnostics;
using Avalonia;

namespace Lumina.Pos.UI;

class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // WinExe has no console by default — attach one in Debug so exceptions are visible.
        // Also always write a crash file next to the exe so silent exits are diagnosable.
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

            // Keep the console open briefly when launched from Explorer
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
            .WithInterFont()
            .LogToTrace();

    private static void AttachConsoleIfPossible()
    {
        try
        {
            // If already attached (dotnet run), this is a no-op / false.
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
