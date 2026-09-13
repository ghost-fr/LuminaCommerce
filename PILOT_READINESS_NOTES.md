# Pilot-Readiness Patch — Real Cart, Confirmed Flows, Demo-Data Safety Guard

## What this closes

1. **Real cart creation** — `IPosSaleService.CreateCartAsync` + `ICartSummary.
   CartId`. This was a genuine, previously-unflagged-until-now gap: there was no
   contract method for starting a cart at all. Confirmed and fixed here, not
   assumed to already work.

2. **Confirmed, not rebuilt** — real register open/close (`IRegisterSessionService`,
   Phase 6) and `CompleteSaleAsync` on the verified VeriFactu path (hardened
   through the correctness pass) were already fully built. No changes needed;
   `PILOT_INTEGRATION_SPEC.md` documents the exact call sequence for Grok.

3. **Device ports — deliberately untouched.** Diffed `DependencyInjection.cs`
   against the Phase 8 version to confirm the `IReceiptPrinter`/`IScaleService`
   registration lines are character-for-character identical. Still emulated,
   nothing about them changed in this patch.

4. **"Turn off local demo pricing for pilot builds" — made concrete and
   enforced, not just documented.** `PilotSafetyGuard` + `IBuildModeProvider`:
   set `LUMINA_BUILD_MODE=Pilot` (or `Production`) and the app refuses to start
   if the database's Tenant NIF matches `Lumina.SeedDev`'s demo default
   (`B12345678`). Development mode (the default when unset) is completely
   unaffected — no local dev workflow changes.

## Why this particular mechanism, and what it does NOT do

I considered a few interpretations of "demo pricing" before settling on this.
The concrete, buildable interpretation: prevent a pilot/production build from
ever running against `Lumina.SeedDev`'s seeded demo tenant, which is the one
realistic way demo data could end up in front of a real customer (someone runs
the seed tool against what's later used as a pilot build's database, by
accident or because a dev DB file gets copied somewhere it shouldn't).

**What it doesn't do:** detect "fake-looking" prices entered through a real
setup flow, or anything requiring judgment about whether data is "real" — that's
not something software can reliably determine, and I didn't want to build a
heuristic that gives false confidence. This is a narrow, mechanical check
against one known constant, not a general demo-detection system. Said plainly:
if you need broader protection than "don't run against the SeedDev default,"
that's a different, larger feature to discuss, not something I want to imply is
already covered.

## Single source of truth for the demo NIF

`PilotSafetyGuard.KnownDemoNif` is now the only place `"B12345678"` is
hardcoded — `Lumina.SeedDev`'s default `--nif` value references this same
constant instead of its own separate literal. They cannot drift apart.

## What's needed to actually activate this

Nothing changes for local dev — `LUMINA_BUILD_MODE` unset means Development,
same as today. For an actual pilot build, someone needs to:
1. Set `LUMINA_BUILD_MODE=Pilot` in that build's environment
2. Seed that pilot's real Tenant with a real NIF (not the SeedDev default) —
   either by hand or via `Lumina.SeedDev --nif <real-value>` for a rehearsal

## Files changed, for a quick diff-focused review

- `Lumina.Contracts.Pos`: `ICartSummary` (+`CartId`), `PosCommands.cs`
  (+`CreateCartRequest`, +`IPosSaleService.CreateCartAsync`)
- `Lumina.Application.Pos.PosSaleService`: `CreateCartAsync` implementation,
  `CartSummary` view gains `CartId`
- New: `Lumina.Application.Ports.IBuildModeProvider`,
  `Lumina.Application.Configuration.PilotSafetyGuard`,
  `Lumina.Infrastructure.Configuration.EnvBuildModeProvider`
- `DependencyInjection.cs`: two new registrations added; device lines
  unchanged (verify via diff if you want the same confidence I have)
- Both `App.axaml.cs`: `PilotSafetyGuard.EnsureSafeAsync()` called at startup
- `Lumina.SeedDev/Program.cs`: default NIF now references the shared constant;
  prints a demo-data warning banner on completion
- `.env.example`: `LUMINA_BUILD_MODE` documented
- New: `docs/PILOT_INTEGRATION_SPEC.md` — hand this to Grok directly
