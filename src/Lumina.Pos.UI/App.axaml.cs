using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lumina.Infrastructure;
using Lumina.Pos.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Lumina.Pos.UI;

/// <summary>
/// Composition root. Local mode loads local-settings.json if tenant env is missing.
/// </summary>
public partial class App : Avalonia.Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static LocalAppSettings LocalSettings { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            LocalSettings = LocalSettingsStore.Load();

            var connectionString =
                Environment.GetEnvironmentVariable("LUMINA_DB_CONNECTION")
                ?? LocalSettings.ConnectionString
                ?? "Data Source=lumina.dev.db";

            var tenantIdRaw =
                Environment.GetEnvironmentVariable("LUMINA_TENANT_ID")
                ?? LocalSettings.TenantId;

            if (!Guid.TryParse(tenantIdRaw, out var tenantId))
            {
                tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                Console.WriteLine("[Lumina.Pos.UI] No tenant — LOCAL MODE. Set TenantId in Configuración or LUMINA_TENANT_ID for full backend.");
            }

            Console.WriteLine($"[Lumina.Pos.UI] Tenant={tenantId}");
            Console.WriteLine($"[Lumina.Pos.UI] DB={connectionString}");
            Console.WriteLine($"[Lumina.Pos.UI] FiscalEnabled={LocalSettings.FiscalEnabled}");
            Console.WriteLine($"[Lumina.Pos.UI] Settings={LocalSettingsStore.FilePath}");

            var services = new ServiceCollection();
            try
            {
                services.AddLuminaBackend(connectionString, tenantId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lumina.Pos.UI] Backend DI issue: {ex.Message}");
            }

            Services = services.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                desktop.MainWindow = new MainWindow();
                Console.WriteLine("[Lumina.Pos.UI] MainWindow created (local-ready).");
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
            }
            catch { }
            throw;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
