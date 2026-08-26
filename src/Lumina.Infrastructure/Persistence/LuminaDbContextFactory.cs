using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lumina.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> tools (migrations, database update).
/// Not used at runtime — runtime wiring goes through DI in DependencyInjection.cs.
/// </summary>
public sealed class LuminaDbContextFactory : IDesignTimeDbContextFactory<LuminaDbContext>
{
    public LuminaDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("LUMINA_DB_CONNECTION")
            ?? "Data Source=lumina.dev.db";

        var options = new DbContextOptionsBuilder<LuminaDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new LuminaDbContext(options);
    }
}
