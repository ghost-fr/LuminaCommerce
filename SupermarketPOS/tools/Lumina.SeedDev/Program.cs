// Lumina.SeedDev — one-shot local dev seed.
//
// Creates a Tenant, Store, TaxCategory (21% general VAT), a Role with broad dev
// capabilities, an admin User (password "changeme" unless overridden), and an
// open Register. Prints LUMINA_TENANT_ID=<guid> at the end — set that as the
// LUMINA_TENANT_ID env var before running either UI app (see
// docs/COMPOSITION_ROOT_NOTES.md).
//
// Idempotent: if a Tenant with the given NIF already exists, this prints its
// existing Id and exits without creating anything new — safe to re-run.
//
// Prerequisite: run `dotnet ef database update` first (see
// docs/PHASE1_NOTES.md) so the schema exists. This tool does not run migrations.
//
// Usage:
//   dotnet run --project tools/Lumina.SeedDev
//   dotnet run --project tools/Lumina.SeedDev -- --nif B87654321 --admin-password s3cret
//
// Options (all optional, sensible defaults):
//   --nif              Tenant NIF, default "B12345678"
//   --legal-name       Tenant legal name, default "Dev Tenant SL"
//   --store-name       Store name, default "Main Store"
//   --admin-username   Admin username, default "admin"
//   --admin-password   Admin password, default "changeme"

using Lumina.Application.Auth;
using Lumina.Domain.Catalogue;
using Lumina.Domain.Identity;
using Lumina.Domain.Sales;
using Lumina.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var options = ParseArgs(args);

var connectionString =
    Environment.GetEnvironmentVariable("LUMINA_DB_CONNECTION") ?? "Data Source=lumina.dev.db";

var dbOptions = new DbContextOptionsBuilder<LuminaDbContext>()
    .UseSqlite(connectionString)
    .Options;

using var db = new LuminaDbContext(dbOptions);

var existing = db.Tenants.FirstOrDefault(t => t.Nif == options.Nif);
if (existing is not null)
{
    Console.WriteLine($"Tenant with NIF {options.Nif} already exists — not creating anything new.");
    Console.WriteLine($"LUMINA_TENANT_ID={existing.Id}");
    return 0;
}

var tenant = new Tenant(Guid.NewGuid(), options.LegalName, options.Nif);
db.Tenants.Add(tenant);

var store = tenant.AddStore(Guid.NewGuid(), options.StoreName, "PerStore");
db.Stores.Add(store);

var taxCategory = new TaxCategory(Guid.NewGuid(), tenant.Id, "General 21%", 0.21m);
db.TaxCategories.Add(taxCategory);

var role = new Role(Guid.NewGuid(), tenant.Id, "Admin", new[]
{
    Capabilities.PosOperateRegister,
    Capabilities.PosOverridePrice,
    Capabilities.PosVoidSale,
    Capabilities.PosIssueRefund,
    Capabilities.StockAdjust,
    Capabilities.StockTransfer,
    Capabilities.AdminManageUsers,
    Capabilities.AdminManageCatalogue,
    Capabilities.AdminManagePricing,
    Capabilities.AdminViewReports,
    Capabilities.FiscalManageVeriFactu,
});
db.Roles.Add(role);

var user = new User(
    Guid.NewGuid(), tenant.Id, options.AdminUsername, options.AdminUsername,
    PasswordHasher.Hash(options.AdminPassword));
user.AssignRole(role.Id);
db.Users.Add(user);

var register = new Register(Guid.NewGuid(), store.Id, "Register 1");
register.Open();
db.Registers.Add(register);

db.SaveChanges();

Console.WriteLine("Seed complete:");
Console.WriteLine($"  Tenant:   {tenant.LegalName} ({tenant.Nif})");
Console.WriteLine($"  Store:    {store.Name}");
Console.WriteLine($"  Register: {register.Name} (open)");
Console.WriteLine($"  Admin:    {options.AdminUsername} / {options.AdminPassword}");
Console.WriteLine();
Console.WriteLine($"LUMINA_TENANT_ID={tenant.Id}");

return 0;

static SeedOptions ParseArgs(string[] args)
{
    var opts = new SeedOptions();
    for (var i = 0; i < args.Length - 1; i++)
    {
        switch (args[i])
        {
            case "--nif": opts.Nif = args[++i]; break;
            case "--legal-name": opts.LegalName = args[++i]; break;
            case "--store-name": opts.StoreName = args[++i]; break;
            case "--admin-username": opts.AdminUsername = args[++i]; break;
            case "--admin-password": opts.AdminPassword = args[++i]; break;
        }
    }
    return opts;
}

sealed class SeedOptions
{
    public string Nif { get; set; } = "B12345678";
    public string LegalName { get; set; } = "Dev Tenant SL";
    public string StoreName { get; set; } = "Main Store";
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "changeme";
}
