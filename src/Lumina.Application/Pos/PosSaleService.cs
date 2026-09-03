using Lumina.Application.Catalogue;
using Lumina.Application.Ports;
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
/// Concrete implementation of Lumina.Contracts.Pos.IPosSaleService.
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
    private readonly ICurrentTenantProvider _tenantProvider;

    public PosSaleService(
        ICartRepository carts, IRegisterRepository registers, ISaleRepository sales,
        IStoreRepository stores, ITenantRepository tenants, IStockAvailabilityChecker stock,
        IFiscalRecordGenerator fiscal, ProductCatalogueService catalogue, IProductRepository products,
        ICurrentTenantProvider tenantProvider)
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
        _tenantProvider = tenantProvider;
    }

    public async Task<Guid> CreateCartAsync(Guid storeId, Guid? customerId = null, CancellationToken ct = default)
    {
        var cart = new Cart(Guid.NewGuid(), storeId);
        if (customerId is not null)
            cart.SetCustomer(customerId);
        await _carts.AddAsync(cart, ct);
        await _carts.SaveChangesAsync(ct);
        return cart.Id;
    }

    public async Task<Guid?> GetOpenRegisterIdAsync(Guid storeId, CancellationToken ct = default)
    {
        var register = await _registers.FindOpenByStoreIdAsync(storeId, ct);
        return register?.Id;
    }

    public async Task<ICartSummary> AddLineAsync(Guid cartId, AddLineRequest request, CancellationToken ct = default)
    {
        var cart = await RequireCartAsync(cartId, ct);

        var product = await _catalogue.FindByBarcodeInternalAsync(_tenantProvider.TenantId, request.Barcode, ct)
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
            if (!await _stock.IsAvailableAsync(group.Key, totalQty, ct))
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

        var sifBoundaryId = store.Id;

        var fiscalResult = await _fiscal.GenerateAsync(
            sifBoundaryId,
            new FiscalRecordRequest(tenant.Nif, ticketNumber, DateOnly.FromDateTime(sale.CompletedAt.Date), sale.Total),
            ct);

        sale.AttachVeriFactuRecord(fiscalResult.VeriFactuRecordId);

        await _sales.AddAsync(sale, ct);
        await _sales.SaveChangesAsync(ct);

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
            "unknown cart id), not a normal business rejection.");

    private async Task<ICartSummary> BuildSummaryAsync(Cart cart, CancellationToken ct)
    {
        var lineViews = new List<ICartLine>();
        foreach (var line in cart.Lines)
        {
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

    private string? BuildQrPayloadForExistingSale(Sale sale) => null;
}

public sealed class ProductNotFoundException : Exception
{
    public ProductNotFoundException(string barcode) : base($"No active product found for barcode '{barcode}'.") { }
}
