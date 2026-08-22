# SupermarketPOS / Lumina Commerce Platform

Phase 0 scaffold — see `/docs/LUMINA_COMMERCE_ARCHITECTURE_BLUEPRINT.md` for the full
architecture and `/docs/CONTRACTS.md` for the frozen POS-sale contract that the backend
(Claude) and UI (Grok) build against in parallel.

## Layout

```
src/Lumina.Domain           Aggregates, value objects, domain events        (backend)
src/Lumina.Application      Commands, handlers, ports                      (backend)
src/Lumina.Infrastructure   EF Core, repositories, outbox                  (backend)
src/Lumina.Fiscal           Hash chain, QR, VeriFactu/AEAT client          (backend)
src/Lumina.Contracts        Shared DTOs/interfaces — the seam              (shared, frozen)
src/Lumina.Pos.UI           Avalonia POS client                            (UI)
src/Lumina.BackOffice.UI    Avalonia back-office app                       (UI)
src/Lumina.Shared.Controls  Reusable Avalonia theme/components             (UI)
tests/                      One test project per backend layer + UI VM tests
```

## Local setup

Requires .NET 8 SDK.

```bash
git clone <repo>
cd SupermarketPOS
cp .env.example .env          # fill in local secrets, never commit .env
dotnet restore
dotnet build
dotnet test
```

This scaffold was authored without a live .NET SDK in the generating environment —
project references and namespaces are correct by hand, but run `dotnet build`
immediately after cloning to catch anything that needs a version bump (target
framework, package versions) for your local SDK.

## Rules of the road

- Backend agent (Claude) does not edit `*.UI` projects' XAML/styling.
- UI agent (Grok) does not edit `Domain`/`Application`/`Infrastructure`/`Fiscal`.
- Changes to `Lumina.Contracts` land in their own small PR, merged before dependent
  backend/UI branches open. See `docs/CONTRACTS.md` for the change process.
- `Lumina.Fiscal.Tests` must pass on every backend PR — it's the one gate that's a
  legal/compliance concern, not just a bug.
