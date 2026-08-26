# Composition Root — DI Wiring

## What this fixes

`Lumina.Pos.UI` and `Lumina.BackOffice.UI` had no entry point (`CS5001`) because they
were empty Avalonia shells from Phase 0 — nobody had written `Program.cs`/`App.axaml.cs`
yet. This patch adds both, plus a single shared DI wiring helper
(`Lumina.Infrastructure.DependencyInjection.AddLuminaBackend`) that both apps call
identically, so the two never drift on what's registered.

Everything from Phase 1-3 is now resolvable via `App.Services` in both UI projects:
`IAuthService`, `IPosSaleService`, `IProductCatalogueService`, and every repository
underneath them.

`MainWindow.axaml` in both projects is a **placeholder** — a single TextBlock saying
so, explicitly marked as Grok's to replace with the real login screen / nav shell /
back-office UI. This exists only so the solution compiles end-to-end; it is not meant
to be built on incrementally, it's meant to be deleted and replaced.

## Known gap: no setup/seed flow yet

`App.axaml.cs` now **requires** an environment variable, `LUMINA_TENANT_ID`, and
throws a clear error on startup if it's missing or not a valid GUID. There's no
onboarding/setup screen yet that would create a Tenant during first-run — that's a
real feature to build later (probably Phase 9-adjacent, or earlier if you want local
dev unblocked sooner — your call).

**For now, to run either app locally:**

```bash
cd src/Lumina.Infrastructure
dotnet ef database update --startup-project ../Lumina.Pos.UI
```

Then insert a Tenant, Store, TaxCategory, and a User by hand — either with a tiny
throwaway C# console snippet (recommended, since `PasswordHasher.Hash(...)` needs to
run in .NET, not raw SQL) or by asking me for a small seed script once migrations
exist. Something like:

```csharp
using var db = new LuminaDbContext(/* options pointed at your local db */);
var tenant = new Tenant(Guid.NewGuid(), "Test Tenant SL", "B12345678");
db.Tenants.Add(tenant);
var store = tenant.AddStore(Guid.NewGuid(), "Main Store", "PerStore");
var taxCategory = new TaxCategory(Guid.NewGuid(), tenant.Id, "General 21%", 0.21m);
db.TaxCategories.Add(taxCategory);
var role = new Role(Guid.NewGuid(), tenant.Id, "Cashier", new[] { Capabilities.PosOperateRegister });
db.Roles.Add(role);
var user = new User(Guid.NewGuid(), tenant.Id, "admin", "Admin", PasswordHasher.Hash("changeme"));
user.AssignRole(role.Id);
db.Users.Add(user);
var register = new Register(Guid.NewGuid(), store.Id, "Register 1");
register.Open();
db.Registers.Add(register);
db.SaveChanges();
Console.WriteLine($"LUMINA_TENANT_ID={tenant.Id}");
```

Set the printed GUID as `LUMINA_TENANT_ID` in your `.env`, and the app should launch.

## Package added

`Lumina.Infrastructure.csproj` gained `Microsoft.Extensions.DependencyInjection`
(8.0.1) — needed for `IServiceCollection`/`AddScoped`/`AddSingleton` in the new
`DependencyInjection.cs`. Nothing else changed on existing packages.
