using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lumina.Application.Configuration;
using Lumina.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Lumina.Pos.UI;

/// <summary>
/// Composition root. Grok's login/nav shell resolves IAuthService, IPosSaleService,
/// and IProductCatalogueService from App.Services — do not construct AuthService,
/// PosSaleService, etc. directly anywhere in UI code, always resolve the interface
/// from here so the DI wiring in Lumina.Infrastructure.DependencyInjection stays the
/// single source of truth.
///
/// UPDATED: now runs PilotSafetyGuard.EnsureSafeAsync() before showing
/// MainWindow — set LUMINA_BUILD_MODE=Pilot (or Production) in a real pilot
/// build's environment for this to actually do anything; it's a no-op in
/// Development (the default when unset), so normal local dev is unaffected.
/// If the guard throws, the app deliberately does not start — see
/// PilotSafetyGuard.cs for exactly what triggers that and why.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // NOTE: reads config from environment variables as a stopgap — a proper
        // config/appsettings.*.json loader hasn't been wired yet (tracked in
        // docs/PHASE2_3_NOTES.md). LUMINA_DB_CONNECTION defaults to a local SQLite
        // file if unset. LUMINA_TENANT_ID has NO default and will throw below if
        // missing — there is no seed/setup flow yet, so for local dev you must
        // manually create a Tenant row and set this env var to its Id.
        var connectionString =
            Environment.GetEnvironmentVariable("LUMINA_DB_CONNECTION") ?? "Data Source=lumina.dev.db";

        var tenantIdRaw = Environment.GetEnvironmentVariable("LUMINA_TENANT_ID");
        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
        {
            throw new InvalidOperationException(
                "LUMINA_TENANT_ID environment variable is not set or is not a valid GUID. " +
                "There is no setup/seed flow yet — for local development, manually insert a " +
                "Tenant row into the SQLite database and set LUMINA_TENANT_ID to its Id before running. " +
                "See docs/PHASE2_3_NOTES.md.");
        }

        services.AddLuminaBackend(connectionString, tenantId);
        Services = services.BuildServiceProvider();

        // Runs synchronously (blocking) before the window shows — this is a
        // single fast DB read, and startup is exactly the right time for a
        // hard-stop safety check to run: before any screen exists to show
        // demo data on. Avalonia's OnFrameworkInitializationCompleted is not
        // async, so GetAwaiter().GetResult() is the deliberate choice here
        // over introducing async-void startup semantics.
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
