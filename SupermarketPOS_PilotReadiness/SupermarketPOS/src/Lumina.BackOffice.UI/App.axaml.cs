using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lumina.Application.Configuration;
using Lumina.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Lumina.BackOffice.UI;

/// <summary>
/// Composition root — same wiring as Lumina.Pos.UI's App.axaml.cs, via the shared
/// Lumina.Infrastructure.DependencyInjection.AddLuminaBackend helper. Keep these
/// two files' registration calls identical; if they diverge, that's a bug.
///
/// UPDATED: same PilotSafetyGuard startup check as Lumina.Pos.UI — see that
/// file's remarks for details.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        var connectionString =
            Environment.GetEnvironmentVariable("LUMINA_DB_CONNECTION") ?? "Data Source=lumina.dev.db";

        var tenantIdRaw = Environment.GetEnvironmentVariable("LUMINA_TENANT_ID");
        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
        {
            throw new InvalidOperationException(
                "LUMINA_TENANT_ID environment variable is not set or is not a valid GUID. " +
                "See docs/PHASE2_3_NOTES.md — same note as Lumina.Pos.UI's App.axaml.cs.");
        }

        services.AddLuminaBackend(connectionString, tenantId);
        Services = services.BuildServiceProvider();

        using (var scope = Services.CreateScope())
        {
            var guard = scope.ServiceProvider.GetRequiredService<PilotSafetyGuard>();
            guard.EnsureSafeAsync().GetAwaiter().GetResult();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
