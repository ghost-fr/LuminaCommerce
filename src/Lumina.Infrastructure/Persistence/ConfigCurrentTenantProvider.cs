using Lumina.Application.Ports;

namespace Lumina.Infrastructure.Persistence;

/// <summary>
/// Reads TenantId from local configuration (config/appsettings.*.json -> Lumina:TenantId,
/// set once at install time for this single-tenant desktop deployment). Composition
/// root wires this via DI: services.AddSingleton&lt;ICurrentTenantProvider&gt;(...).
/// </summary>
public sealed class ConfigCurrentTenantProvider : ICurrentTenantProvider
{
    public Guid TenantId { get; }

    public ConfigCurrentTenantProvider(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException(
                "TenantId must be set in local config before the app starts. " +
                "This is normally seeded once during Phase 1 install/setup flow.",
                nameof(tenantId));
        TenantId = tenantId;
    }
}
