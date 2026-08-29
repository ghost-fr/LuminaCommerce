using Lumina.Application.Catalogue;
using Lumina.Application.Ports;
using Lumina.Application.Stock;
using Lumina.Contracts.Pos;
using Lumina.Domain.Sales;

namespace Lumina.Application.Pos;

internal sealed class CartSummary : ICartSummary
{
    public IReadOnlyList<ICartLine> Lines { get; init; } = Array.Empty<ICartLine>();
    public decimal Subtotal { get; init; }
    public decimal VatTotal { get; init; }
    public decimal Total { get; init; }
    public Guid? CustomerId { get; init; }
}

internal sealed class CartLineView : ICartLine
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Barcode { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public decimal Quantity { get; init; }
    public decimal VatRate { get; init; }
    public decimal LineTotal { get; init; }
    public string? AppliedPromotionCode { get; init; }
}

/// <summary>
/// Concrete implementation of Lumina.Contracts.Pos.IPosSaleService — the fiscal-
/// critical orchestration for Phase 3. See docs/CONTRACTS.md §2 for the contract
/// this must satisfy; UI-visible behavior (what each CompleteSaleStatus means,
/// idempotency semantics) is documented there, not repeated here.
/// </summary>
public sealed class PosSaleService : IPosSaleService
{
    private readonly ICartRepository _carts;
    private readonly IRegisterRepository _registers;
    private readonly ISaleRepository _sales;
    private readonly IStoreRepository _stores;
    private readonly ITenantRepository _tenants;
    private readonly IStockAvailabilityChecker _stock;
    private readonly IFiscalRecordGenerator _fiscal;
    private readonly ProductCatalogueService _catalogue;
    private readonly IProductRepository _products;
    private readonly StockLedgerService _stockLedger;

    public PosSaleService(
        ICartRepository carts, IRegisterRepository registers, ISaleRepository sales,
        IStoreRepository stores, ITenantRepository tenants, IStockAvailabilityChecker stock,
        IFiscalRecordGenerator fiscal, ProductCatalogueService catalogue, IProductRepository products,
        StockLedgerService stockLedger)
    {
        _carts = carts;
        _registers = registers;
        _sales = sales;
        _stores = stores;
        _tenants = tenants;
        _stock = stock;
        _fiscal = fiscal;
        _catalogue = catalogue;
        _products = products;
        _stockLedger = stockLedger;
    }

    public async Task<ICartSummary> AddLineAsync(Guid cartId, AddLineRequest request, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(cartId, ct);

        var product = await _catalogue.FindByBarcodeAsync(request.Barcode, ct)
            ?? throw new ProductNotFoundException(request.Barcode);

        cart.AddLine(
            product.ProductId, request.Quantity, product.CurrentPrice, product.VatRate,
            product.HasActivePromotion ? "ACTIVE_PROMO" : null);

        await _carts.UpdateAsync(cart, ct);
        await _carts.SaveChangesAsync(ct);
        return await BuildSummaryAsync(cart, ct);
    }

    public async Task<ICartSummary> RemoveLineAsync(Guid cartId, RemoveLineRequest request, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(cartId, ct);
        cart.RemoveQuantity(request.ProductId, request.Quantity);
        await _carts.UpdateAsync(cart, ct);
        await _carts.SaveChangesAsync(ct);
        return await BuildSummaryAsync(cart, ct);
    }

    public async Task<CompleteSaleResult> CompleteSaleAsync(
        Guid cartId, CompleteSaleRequest request, CancellationToken ct = default)
    {
        // Idempotency check FIRST, before any other validation — a retried request
        // with a previously-successful key must short-circuit straight to the
        // original result, never re-run stock/tender checks against what may now
        // be a different cart state.
        var existing = await _sales.FindByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existing is not null)
        {
            return new CompleteSaleResult(
                CompleteSaleStatus.Success, existing.Id, existing.TicketNumber,
                BuildQrPayloadForExistingSale(existing), RejectionCode.None, null);
        }

        var cart = await RequireCartAsync(cartId, ct);

        var register = await _registers.FindByIdAsync(request.RegisterId, ct);
        if (register is null || !register.IsOpen)
        {
            return Rejected(RejectionCode.RegisterNotOpen, "The selected register is not open.");
        }

        if (cart.Lines.Count == 0)
        {
            return Rejected(RejectionCode.Unknown, "Cart is empty — nothing to sell.");
        }

        foreach (var group in cart.Lines.GroupBy(l => l.ProductId))
        {
            var totalQty = group.Sum(l => l.Quantity);
            if (!await _stock.IsAvailableAsync(cart.StoreId, group.Key, totalQty, ct))
                return Rejected(RejectionCode.StockUnavailable, $"Insufficient stock for product {group.Key}.");
        }

        var tenderTotal = request.Tenders.Sum(t => t.Amount);
        if (tenderTotal != cart.Total)
        {
            return Rejected(RejectionCode.PaymentValidationFailed,
                $"Tender total ({tenderTotal}) does not match sale total ({cart.Total}).");
        }

        var store = await _stores.FindByIdAsync(cart.StoreId, ct)
            ?? throw new InvalidOperationException($"Cart {cartId} references Store {cart.StoreId} which does not exist.");
        var tenant = await _tenants.FindByIdAsync(store.TenantId, ct)
            ?? throw new InvalidOperationException($"Store {store.Id} references Tenant {store.TenantId} which does not exist.");

        var ticketNumber = await _sales.NextTicketNumberAsync(store.Id, ct);

        var saleLines = cart.Lines.Select(l => new SaleLine(
            l.ProductId, l.Quantity, l.UnitPrice, l.VatRate, l.LineSubtotal, l.LineVat, l.LineTotal, l.PromotionCode)).ToList();
        var saleTenders = request.Tenders.Select(t => new SaleTender(t.Type.ToString(), t.Amount, t.Reference)).ToList();

        var sale = new Sale(
            Guid.NewGuid(), store.Id, register.Id, request.CustomerId, ticketNumber,
            saleLines, saleTenders, request.IdempotencyKey);

        // SIF boundary: PerStore per config/appsettings default (see docs/CONTRACTS.md
        // §0 / blueprint §8.4). If a tenant's config uses PerCompany or PerTerminal
        // instead, this must resolve to store.TenantId or a terminal id respectively —
        // wiring that config lookup is a Phase 9 hardening item, flagged not guessed.
        var sifBoundaryId = store.Id;

        var fiscalResult = await _fiscal.GenerateAsync(
            sifBoundaryId,
            new FiscalRecordRequest(tenant.Nif, ticketNumber, DateOnly.FromDateTime(sale.CompletedAt.Date), sale.Total),
            ct);

        sale.AttachVeriFactuRecord(fiscalResult.VeriFactuRecordId);

        // Stock movements recorded in the SAME unit of work as the Sale and the
        // fiscal chain link (all share one DbContext scope) — one final
        // SaveChangesAsync below commits Sale + StockMovements + VeriFactuChainRecord
        // together. If this call were after SaveChangesAsync instead, a crash
        // between the two would leave a completed, fiscally-recorded sale with NO
        // corresponding stock decrement — silently wrong inventory forever.
        await _stockLedger.RecordSaleMovementsAsync(
            store.Id, sale.Id, cart.Lines.Select(l => (l.ProductId, l.Quantity)), ct);

        await _sales.AddAsync(sale, ct);
        await _sales.SaveChangesAsync(ct);

        // NOTE: AEAT submission (VERI*FACTU outbox) is Phase 9 scope, not yet wired.
        // Every sale currently returns Success, never SuccessPendingSubmission —
        // correct for now since nothing is actually queued for async submission yet,
        // but revisit this return value the moment the outbox lands in Phase 9.
        return new CompleteSaleResult(
            CompleteSaleStatus.Success, sale.Id, sale.TicketNumber,
            fiscalResult.QrPayload, RejectionCode.None, null);
    }

    private static CompleteSaleResult Rejected(RejectionCode code, string reason) =>
        new(CompleteSaleStatus.Rejected, null, null, null, code, reason);

    private async Task<Cart> RequireCartAsync(Guid cartId, CancellationToken ct) =>
        await _carts.FindByIdAsync(cartId, ct)
        ?? throw new InvalidOperationException(
            $"Cart {cartId} was not found. This indicates a UI/session bug (calling with a stale or " +
            "unknown cart id), not a normal business rejection — it should never surface to the operator " +
            "as a 'sale rejected' message.");

    private async Task<ICartSummary> BuildSummaryAsync(Cart cart, CancellationToken ct)
    {
        var lineViews = new List<ICartLine>();
        foreach (var line in cart.Lines)
        {
            // Product name/barcode looked up directly via IProductRepository — cheap,
            // single-entity read. Price/VAT come from the cart line's own snapshot
            // (correctly re-resolved at add-time per CONTRACTS.md §1), not re-fetched
            // here, so a display-name lookup can never accidentally change the price
            // the operator already sees.
            var product = await _products.FindByIdAsync(line.ProductId, ct);
            lineViews.Add(new CartLineView
            {
                ProductId = line.ProductId,
                ProductName = product?.Name ?? "(unknown product)",
                Barcode = product?.Barcode ?? "",
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                VatRate = line.VatRate,
                LineTotal = line.LineTotal,
                AppliedPromotionCode = line.PromotionCode
            });
        }

        return new CartSummary
        {
            Lines = lineViews,
            Subtotal = cart.Subtotal,
            VatTotal = cart.VatTotal,
            Total = cart.Total,
            CustomerId = cart.CustomerId
        };
    }

    private string? BuildQrPayloadForExistingSale(Sale sale) =>
        // On idempotent replay we don't have the original QR payload stored on the
        // Sale aggregate itself (only VeriFactuRecordId). Returning null here is a
        // known gap — flagged in docs/PHASE2_3_NOTES.md — fix is to either store the
        // QR payload on Sale directly, or add IVeriFactuChainStore.GetQrPayload(id).
        // Not fixed now to avoid guessing at which approach fits the eventual ticket
        // reprint feature (Phase 6/7 territory) better.
        null;
}

public sealed class ProductNotFoundException : Exception
{
    public ProductNotFoundException(string barcode) : base($"No active product found for barcode '{barcode}'.") { }
}
