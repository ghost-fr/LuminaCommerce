using Lumina.Application.Ports;
using Lumina.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Lumina.Infrastructure.Persistence.Repositories;

public class RegisterRepository : IRegisterRepository
{
    private readonly LuminaDbContext _db;
    public RegisterRepository(LuminaDbContext db) => _db = db;

    public Task<Register?> FindByIdAsync(Guid registerId, CancellationToken ct = default) =>
        _db.Registers.FirstOrDefaultAsync(r => r.Id == registerId, ct);
}
