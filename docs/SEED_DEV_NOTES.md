# Lumina.SeedDev — local dev seed tool

Fills the gap: there's no onboarding/setup screen yet, so `App.axaml.cs` throws on
startup without a `LUMINA_TENANT_ID` env var pointing at a real Tenant row. This
console tool creates one.

## First-time setup

```bash
# From repo root

# 1. Make sure migrations are applied first
cd src/Lumina.Infrastructure
dotnet ef database update --startup-project ../Lumina.Pos.UI
cd ../..

# 2. Run the seed
dotnet run --project tools/Lumina.SeedDev
```

Output ends with a line like:

```
LUMINA_TENANT_ID=3f2a1c9e-4b7d-4e8a-9c1f-6d2b5a8e0f31
```

Copy that into your environment as `LUMINA_TENANT_ID=...`. Default admin login is
`admin` / `changeme`.

## Options

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
existing Id and exits without creating anything new.

## What it creates

One Tenant, one Store (`SifBoundary: PerStore`), one `TaxCategory` (General 21%),
one `Role` named "Admin" with every currently-defined capability, one `User` with
that role, and one open `Register`.
