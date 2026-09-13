using Lumina.Application.Configuration;
using Lumina.Application.Ports;
using Lumina.Domain.Identity;
using Xunit;

namespace Lumina.Application.Tests.Configuration;

public class PilotSafetyGuardTests
{
    private sealed class FakeBuildModeProvider : IBuildModeProvider
    {
        public BuildMode Mode { get; init; }
    }

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public Tenant? Tenant;
        public Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Tenant?.Id == tenantId ? Tenant : null);
    }

    private sealed class FakeTenantProvider : ICurrentTenantProvider
    {
        public Guid TenantId { get; init; }
    }

    [Fact]
    public async Task EnsureSafeAsync_DevelopmentMode_NeverChecks_EvenWithDemoNif()
    {
        var tenantId = Guid.NewGuid();
        var tenants = new FakeTenantRepository
        {
            Tenant = new Tenant(tenantId, "Demo Co", PilotSafetyGuard.KnownDemoNif)
        };
        var guard = new PilotSafetyGuard(
            new FakeBuildModeProvider { Mode = BuildMode.Development },
            tenants,
            new FakeTenantProvider { TenantId = tenantId });

        // Should NOT throw — Development mode is never checked, by design.
        await guard.EnsureSafeAsync();
    }

    [Fact]
    public async Task EnsureSafeAsync_PilotMode_DemoNif_Throws()
    {
        var tenantId = Guid.NewGuid();
        var tenants = new FakeTenantRepository
        {
            Tenant = new Tenant(tenantId, "Demo Co", PilotSafetyGuard.KnownDemoNif)
        };
        var guard = new PilotSafetyGuard(
            new FakeBuildModeProvider { Mode = BuildMode.Pilot },
            tenants,
            new FakeTenantProvider { TenantId = tenantId });

        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.EnsureSafeAsync());
    }

    [Fact]
    public async Task EnsureSafeAsync_ProductionMode_DemoNif_Throws()
    {
        var tenantId = Guid.NewGuid();
        var tenants = new FakeTenantRepository
        {
            Tenant = new Tenant(tenantId, "Demo Co", PilotSafetyGuard.KnownDemoNif)
        };
        var guard = new PilotSafetyGuard(
            new FakeBuildModeProvider { Mode = BuildMode.Production },
            tenants,
            new FakeTenantProvider { TenantId = tenantId });

        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.EnsureSafeAsync());
    }

    [Fact]
    public async Task EnsureSafeAsync_PilotMode_RealNif_DoesNotThrow()
    {
        var tenantId = Guid.NewGuid();
        var tenants = new FakeTenantRepository
        {
            Tenant = new Tenant(tenantId, "Tienda Real SL", "B87654321") // NOT the demo NIF
        };
        var guard = new PilotSafetyGuard(
            new FakeBuildModeProvider { Mode = BuildMode.Pilot },
            tenants,
            new FakeTenantProvider { TenantId = tenantId });

        await guard.EnsureSafeAsync(); // should not throw
    }

    [Fact]
    public async Task EnsureSafeAsync_PilotMode_TenantNotFound_DoesNotThrow()
    {
        // A missing tenant is a different, already-handled startup failure
        // (App.axaml.cs's LUMINA_TENANT_ID validation) — PilotSafetyGuard
        // should not be the one to surface that, and should not throw here.
        var tenants = new FakeTenantRepository { Tenant = null };
        var guard = new PilotSafetyGuard(
            new FakeBuildModeProvider { Mode = BuildMode.Pilot },
            tenants,
            new FakeTenantProvider { TenantId = Guid.NewGuid() });

        await guard.EnsureSafeAsync(); // should not throw
    }
}
