# Lumina.SeedDev — local dev seed tool

Fills the gap flagged in `docs/COMPOSITION_ROOT_NOTES.md`: there's no onboarding/
setup screen yet, so `App.axaml.cs` throws on startup without a `LUMINA_TENANT_ID`
env var pointing at a real Tenant row. This console tool creates one.

## First-time setup

```bash
# 1. Add the project to the solution (one-time)
dotnet sln add tools/Lumina.SeedDev/Lumina.SeedDev.csproj

# 2. Make sure migrations are applied first
cd src/Lumina.Infrastructure
dotnet ef database update --startup-project ../Lumina.Pos.UI
cd ../..

# 3. Run the seed
dotnet run --project tools/Lumina.SeedDev
```

Output ends with a line like:

```
LUMINA_TENANT_ID=3f2a1c9e-4b7d-4e8a-9c1f-6d2b5a8e0f31
```

Copy that into your `.env` as `LUMINA_TENANT_ID=...`. Default admin login is
`admin` / `changeme` — change it (or pass `--admin-password` at seed time) before
this ever points at anything real.

## Options

All optional, override any subset:

```bash
dotnet run --project tools/Lumina.SeedDev -- \
  --nif B87654321 \
  --legal-name "My Real Company SL" \
  --store-name "Valencia Centro" \
  --admin-username ahmad \
  --admin-password "something-not-changeme"
```

## Idempotent — safe to re-run

If a Tenant with the given `--nif` already exists, the tool prints that Tenant's
existing Id and exits without creating anything new. Re-running with the same NIF
after the DB already has that tenant will not create duplicates or a second admin
user — but it also won't apply a changed `--admin-password` to an existing tenant;
that's a manual DB edit or a future "reset dev data" command, not handled here.

## What it creates

One Tenant, one Store (`SifBoundary: PerStore`), one `TaxCategory` (General 21%),
one `Role` named "Admin" with every currently-defined capability (this is a dev
convenience — a real admin role provisioning flow should be more deliberate about
what it grants), one `User` with that role, and one open `Register`.
