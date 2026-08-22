# Phase 1 — Foundation: what's here, what's not

## Delivered

- `Lumina.Domain.Identity`: `Tenant`, `Store`, `User`, `Role`, well-known `Capabilities`
  constants. Rich domain objects with invariants (no empty usernames, idempotent role
  assignment, etc.) per the blueprint's "no DataRows" principle.
- `Lumina.Contracts.Auth`: `IAuthService`, `IUserSession`, `LoginRequest`/`LoginResult` —
  the seam. UI (Grok) can build a login screen and capability-gated nav shell against
  this today.
- `Lumina.Application.Auth`: `AuthService` (concrete `IAuthService`), `PasswordHasher`
  (PBKDF2-SHA256, no external package), `UserSession`.
- `Lumina.Application.Ports`: `IUserRepository`, `IRoleRepository`, `IStoreRepository`,
  `ICurrentTenantProvider` — backend-internal interfaces `AuthService` depends on,
  implemented by Infrastructure. This is what makes `AuthServiceTests` runnable with
  in-memory fakes instead of a real database (see tests/Lumina.Application.Tests/Auth).
- `Lumina.Infrastructure.Persistence`: `LuminaDbContext`, EF Core entity configurations
  for Tenant/Store/User/Role, repository implementations, `ConfigCurrentTenantProvider`.
- Unit tests: `UserTests`, `RoleTests` (Domain), `AuthServiceTests` (Application, 5 cases
  covering success, wrong password, unknown user, inactive user, logout).

## Deliberately NOT delivered here — needs your local machine

**EF Core migration files.** I don't have the .NET SDK / `dotnet ef` tool available in
this sandbox, so I did not hand-write migration files — EF Core migrations include
generated designer/snapshot code that's easy to get subtly wrong by hand and would
likely fail to apply. Instead, once you pull this in:

```bash
dotnet tool install --global dotnet-ef   # if not already installed
cd src/Lumina.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../Lumina.Pos.UI
dotnet ef database update --startup-project ../Lumina.Pos.UI
```

If that fails to build first, most likely cause is the `User.RoleIds` / `Role.Capabilities`
CSV-column mapping flagged in `UserConfiguration.cs` — the comment there explains the
two ways to finish it (value converter vs. join table). I'd recommend the join table;
it's cleaner and this is exactly the kind of thing worth getting right before Phase 3
builds on top of it.

**`ConfigCurrentTenantProvider` wiring.** The class exists but nothing calls it yet —
someone (me, next) needs to add the composition-root code in each UI project's
`App.axaml.cs` that reads `TenantId` from `config/appsettings.*.json` and registers
`ConfigCurrentTenantProvider` in DI. Small, but it's the thing that turns this from
"compiles" to "runs."

## Suggested next move

Either: (a) I write the DI composition root + a minimal login screen contract Grok can
build the actual XAML against, or (b) you run the migration commands above first so
there's a real local DB to test against, then I do (a). Your call.
