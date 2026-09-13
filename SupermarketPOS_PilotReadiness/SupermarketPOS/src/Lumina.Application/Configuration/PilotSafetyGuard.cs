using Lumina.Application.Ports;

namespace Lumina.Application.Configuration;

/// <summary>
/// Refuses to let the app proceed past startup in a Pilot/Production build if
/// the loaded Tenant looks like Lumina.SeedDev's demo tenant — a concrete,
/// enforced answer to "turn off local demo pricing for pilot builds," rather
/// than a policy left to remember. Development builds are never checked, so
/// local dev/seeding workflows are completely unaffected.
///
/// What this catches: someone runs Lumina.SeedDev against a database that's
/// later used for a Pilot/Production build (by accident, or because a dev
/// environment's DB file was copied into a pilot build's config by mistake).
/// What this does NOT catch: demo/placeholder VALUES entered by hand through
/// a real setup flow using a real-looking NIF — this check is specifically
/// about the SeedDev tool's known default, not a general "is this data fake"
/// heuristic, which isn't something software can reliably determine.
/// </summary>
public sealed class PilotSafetyGuard
{
    /// <summary>The exact default NIF Lumina.SeedDev uses unless overridden
    /// via --nif. Single source of truth — Lumina.SeedDev's Program.cs
    /// references this same constant rather than a separately hardcoded
    /// literal, so the two can never drift apart.</summary>
    public const string KnownDemoNif = "B12345678";

    private readonly IBuildModeProvider _buildMode;
    private readonly ITenantRepository _tenants;
    private readonly ICurrentTenantProvider _tenantProvider;

    public PilotSafetyGuard(IBuildModeProvider buildMode, ITenantRepository tenants, ICurrentTenantProvider tenantProvider)
    {
        _buildMode = buildMode;
        _tenants = tenants;
        _tenantProvider = tenantProvider;
    }

    /// <summary>Called once at app startup, after the DI container is built
    /// and before the main window is shown (see App.axaml.cs in both UI
    /// projects). Throws — a hard stop, not a warning — if this looks like
    /// demo data in a non-Development build. Does nothing at all in
    /// Development mode.</summary>
    public async Task EnsureSafeAsync(CancellationToken ct = default)
    {
        if (_buildMode.Mode == BuildMode.Development) return;

        var tenant = await _tenants.FindByIdAsync(_tenantProvider.TenantId, ct);
        if (tenant is null) return; // a missing tenant is a different, already-handled startup failure

        if (tenant.Nif == KnownDemoNif)
        {
            throw new InvalidOperationException(
                $"Refusing to start in {_buildMode.Mode} mode: this database's Tenant NIF matches " +
                $"Lumina.SeedDev's default demo value ({KnownDemoNif}). This looks like demo/seed data " +
                "running in a non-Development build. Pilot and Production builds must never run against " +
                "demo data. If you're deliberately rehearsing a pilot with test data, seed a Tenant with " +
                "a different NIF (Lumina.SeedDev's --nif option) rather than the default.");
        }
    }
}
