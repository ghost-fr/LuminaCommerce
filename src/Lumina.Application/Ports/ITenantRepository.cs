using Lumina.Domain.Identity;

namespace Lumina.Application.Ports;

public interface ITenantRepository
{
    Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken ct = default);
}
