using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lumina.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Lumina.Pos.UI;

/// <summary>
/// Composition root. UI resolves IAuthService / IPosSaleService / etc. from App.Services.
/// </summary>
public partial class App : Avalonia.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var services = new ServiceCollection();

            var connectionString =
                Environment.GetEnvironmentVariable("LUMINA_DB_CONNECTION") ?? "Data Source=lumina.dev.db";

            var tenantIdRaw = Environment.GetEnvironmentVariable("LUMINA_TENANT_ID");
            if (!Guid.TryParse(tenantIdRaw, out var tenantId))
            {
                throw new InvalidOperationException(
                    "LUMINA_TENANT_ID environment variable is not set or is not a valid GUID. " +
                    "Run tools/Lumina.SeedDev and set LUMINA_TENANT_ID to the printed Id.");
            }

            Console.WriteLine($"[Lumina.Pos.UI] Tenant={tenantId}");
            Console.WriteLine($"[Lumina.Pos.UI] DB={connectionString}");

            services.AddLuminaBackend(connectionString, tenantId);
            Services = services.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                desktop.MainWindow = new MainWindow();
                Console.WriteLine("[Lumina.Pos.UI] MainWindow created.");
            }
            else
            {
                Console.WriteLine("[Lumina.Pos.UI] WARNING: not a classic desktop lifetime — no MainWindow.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[Lumina.Pos.UI] Startup failed:");
            Console.Error.WriteLine(ex);
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "pos-crash.log");
                File.WriteAllText(path, $"{DateTimeOffset.Now:o}\n{ex}\n");
                Console.Error.WriteLine($"Wrote {path}");
            }
            catch { }
            throw; // rethrow so Program.Main can keep the console open
        }

        base.OnFrameworkInitializationCompleted();
    }
}
