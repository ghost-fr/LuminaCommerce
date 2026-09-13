# Pilot-Readiness Integration Spec (for Grok)

This is the exact, unambiguous wiring for three real flows: opening a register,
building a cart from real catalogue data, and completing a sale on the verified
VeriFactu path. Everything below is now backend-real — no method listed here is a
stub or a placeholder. If any screen currently uses hardcoded/mock data for these
three things, that's what this spec replaces.

All interfaces are in `Lumina.Contracts.*`, resolved via `App.Services` (see
`App.axaml.cs`) — never construct a concrete `Application.*` class directly from UI
code.

## 1. Real register open

```csharp
var registerSessionService = App.Services.GetRequiredService<IRegisterSessionService>();

var result = await registerSessionService.OpenAsync(
    new OpenRegisterRequest(RegisterId: selectedRegisterId, OpeningFloat: cashCountedByOperator));

if (result.Status == OpenRegisterStatus.Success)
{
    // result.Session.RegisterSessionId — keep this around; it's what
    // IReportingService.GetRegisterSessionReportAsync uses for the X/Z report screen.
}
else
{
    // result.RejectionReason — e.g. "Register is already open."
}
```

No sale can complete against a register that isn't open (`RejectionCode.
RegisterNotOpen` on `CompleteSaleAsync` otherwise) — this was already true, now it's
backed by a real open `RegisterSession`, not just a boolean flag.

**Closing** at end of shift:
```csharp
var closeResult = await registerSessionService.CloseAsync(
    new CloseRegisterRequest(RegisterId: registerId, DeclaredClosingCash: countedCash));
// closeResult.ExpectedClosingCash, closeResult.Discrepancy — show both, always.
```

## 2. Real cart from catalogue

This is the flow that was actually missing a piece — `CreateCartAsync` didn't
exist until this patch. Full flow:

```csharp
var posSaleService = App.Services.GetRequiredService<IPosSaleService>();
var catalogueService = App.Services.GetRequiredService<IProductCatalogueService>();

// Step 1 — start the cart (once per sale)
var cart = await posSaleService.CreateCartAsync(
    new CreateCartRequest(StoreId: currentSession.StoreId, CustomerId: null));
var cartId = cart.CartId; // NEW field — keep this for every subsequent call

// Step 2 — barcode scan (scanner types into this field automatically, see
// CONTRACTS.md §7.1 — no scanner integration code needed)
var scanned = await catalogueService.FindByBarcodeAsync(scannedBarcode);
if (scanned is null)
{
    // "product not found" — do NOT fabricate a line
}
else
{
    var updated = await posSaleService.AddLineAsync(cartId, new AddLineRequest(scannedBarcode, quantity: 1));
    // updated.Lines, updated.Subtotal, updated.VatTotal, updated.Total — bind directly, all real
}

// Step 2b — product search/browse (if the screen has a search box, not just scan)
var searchResults = await catalogueService.SearchAsync(new ProductSearchRequest(Query: searchText));
// searchResults.Items — each is IProductSummary with real CurrentPrice/OriginalPrice
```

**No demo/hardcoded product list should back this screen going forward.** Every
price shown must come from `IProductSummary.CurrentPrice` (already promotion-
adjusted server-side — never recompute a discount client-side) or from
`ICartLine.LineTotal` after `AddLineAsync`.

## 3. Complete sale on the verified VeriFactu path

```csharp
var tenders = new List<TenderLine> { new(TenderType.Cash, amountTendered) };
// Real payment — split Cash/Card into SEPARATE TenderLines if mixed. Do NOT use
// TenderType.MixedCashCard on a single line — it's rejected outright
// (RejectionCode.PaymentValidationFailed) as of Phase 6. See CONTRACTS.md §5.2 —
// if the current tender screen has one "Mixto" button producing a single combined
// line, that needs to become two amount fields.

var idempotencyKey = Guid.NewGuid(); // generate ONCE per submission attempt, reuse on retry

var result = await posSaleService.CompleteSaleAsync(
    cartId,
    new CompleteSaleRequest(RegisterId: registerId, CustomerId: null, Tenders: tenders, idempotencyKey));

switch (result.Status)
{
    case CompleteSaleStatus.Success:
    case CompleteSaleStatus.SuccessPendingSubmission: // treat identically — see CONTRACTS.md §2.2
        // result.TicketNumber, result.QrPayload are REAL — from a verified,
        // fixed-vector-tested SHA-256 chain (see HASH_CHAIN_FIX_NOTES.md) and
        // the chain-integrity pre-check (GAPFIXES_NOTES.md). Print the ticket,
        // clear the cart.
        break;
    case CompleteSaleStatus.Rejected:
        // result.RejectionCode + result.RejectionReason — show a distinct message
        // per code (CONTRACTS.md §2). Do NOT print anything, do NOT clear the cart.
        // RejectionCode.FiscalChainError is now real and reachable — give it its
        // own message, don't lump it in with generic errors (CONTRACTS.md §2.5).
        break;
}
```

**On retry after a timeout/dropped connection:** reuse the SAME `idempotencyKey`,
never generate a new one. Known gap, unchanged: a retried call currently returns
`QrPayload: null` — if you need to reprint after a retry, that's an open item, not
yet solved (see `PHASE2_3_NOTES.md` item 8).

## Device ports — unchanged, still emulated

`IReceiptPrinter`/`IScaleService` were **not touched** in this patch, per explicit
instruction to keep them stable. They're still emulated (`SimulatedReceiptPrinter`/
`SimulatedScaleService`) — nothing about real printing or real scale reads changed.
See `PHASE8_NOTES.md` if you need the current state of those.

## Pilot builds — one new thing to know about

If/when a real pilot build gets its own environment (`LUMINA_BUILD_MODE=Pilot`),
the app will **refuse to start** if the database's Tenant NIF matches
`Lumina.SeedDev`'s demo default. This is intentional — it's the backend half of
"turn off local demo pricing for pilot builds." Nothing for UI to do here, but
worth knowing so an unexpected startup crash during pilot setup isn't mysterious —
check the Tenant's NIF in that database first.
