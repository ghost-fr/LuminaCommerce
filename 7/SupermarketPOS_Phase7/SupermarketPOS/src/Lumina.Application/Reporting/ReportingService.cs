using Lumina.Application.Ports;
using Lumina.Contracts.Pos;
using Lumina.Contracts.Reporting;
using Lumina.Domain.Cash;
using Lumina.Domain.Sales;

namespace Lumina.Application.Reporting;

internal sealed class DailySalesReportView : IDailySalesReport
{
    public Guid StoreId { get; init; }
    public DateOnly Date { get; init; }
    public int SaleCount { get; init; }
    public decimal GrossSubtotal { get; init; }
    public decimal VatTotal { get; init; }
    public decimal GrandTotal { get; init; }
    public decimal CashTotal { get; init; }
    public decimal CardTotal { get; init; }
    public decimal OtherTenderTotal { get; init; }
}

internal sealed class ProductSalesLineView : IProductSalesLine
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal QuantitySold { get; init; }
    public decimal RevenueTotal { get; init; }
}

internal sealed class StockOnHandLineView : IStockOnHandLine
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal QuantityOnHand { get; init; }
}

internal sealed class RegisterSessionReportView : IRegisterSessionReport
{
    public Guid RegisterSessionId { get; init; }
    public Guid RegisterId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset OpenedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public decimal OpeningFloat { get; init; }
    public decimal CashSalesTotal { get; init; }
    public decimal CashInTotal { get; init; }
    public decimal CashOutTotal { get; init; }
    public decimal LiveExpectedCash { get; init; }
    public decimal? DeclaredClosingCash { get; init; }
    public decimal? Discrepancy { get; init; }
}

/// <summary>
/// Concrete implementation of Lumina.Contracts.Reporting.IReportingService.
/// Every report is computed LIVE by querying and aggregating the actual
/// persisted Sale/CashMovement/StockMovement records at request time — there is
/// deliberately no cached, denormalized, or separately-maintained running total
/// anywhere in this class. This is what makes "daily report matches raw sale
/// data" true by construction: there is only one source of truth, and every
/// report reads directly from it.
/// </summary>
public sealed class ReportingService : IReportingService
{
    private readonly ISaleRepository _sales;
    private readonly IProductRepository _products;
    private readonly IStockLedgerRepository _stockLedger;
    private readonly IStoreRepository _stores;
    private readonly IRegisterSessionRepository _registerSessions;
    private readonly ICashMovementRepository _cashMovements;

    public ReportingService(
        ISaleRepository sales, IProductRepository products, IStockLedgerRepository stockLedger,
        IStoreRepository stores, IRegisterSessionRepository registerSessions, ICashMovementRepository cashMovements)
    {
        _sales = sales;
        _products = products;
        _stockLedger = stockLedger;
        _stores = stores;
        _registerSessions = registerSessions;
        _cashMovements = cashMovements;
    }

    public async Task<IDailySalesReport> GetDailySalesReportAsync(
        DailySalesReportRequest request, CancellationToken ct = default)
    {
        var sales = await GetSalesForLocalDateAsync(request.StoreId, request.Date, ct);

        decimal cashTotal = 0m, cardTotal = 0m, otherTotal = 0m;
        foreach (var sale in sales)
        {
            foreach (var tender in sale.Tenders)
            {
                // Tender.TenderType is stored as a string on SaleTender (see
                // Domain.Sales.Sale) — parsed back here rather than assuming a
                // specific casing/format, to stay resilient to how PosSaleService
                // serialized it (ToString() on the Contracts.Pos.TenderType enum).
                if (Enum.TryParse<TenderType>(tender.TenderType, out var type))
                {
                    switch (type)
                    {
                        case TenderType.Cash: cashTotal += tender.Amount; break;
                        case TenderType.Card: cardTotal += tender.Amount; break;
                        default: otherTotal += tender.Amount; break;
                    }
                }
                else
                {
                    otherTotal += tender.Amount;
                }
            }
        }

        return new DailySalesReportView
        {
            StoreId = request.StoreId,
            Date = request.Date,
            SaleCount = sales.Count,
            GrossSubtotal = sales.Sum(s => s.Subtotal),
            VatTotal = sales.Sum(s => s.VatTotal),
            GrandTotal = sales.Sum(s => s.Total),
            CashTotal = cashTotal,
            CardTotal = cardTotal,
            OtherTenderTotal = otherTotal
        };
    }

    public async Task<IReadOnlyList<IProductSalesLine>> GetTopProductsAsync(
        TopProductsRequest request, CancellationToken ct = default)
    {
        var sales = await GetSalesForLocalDateAsync(request.StoreId, request.Date, ct);

        var byProduct = sales
            .SelectMany(s => s.Lines)
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(l => l.Quantity), Revenue = g.Sum(l => l.LineTotal) })
            .OrderByDescending(x => x.Revenue)
            .Take(request.Limit)
            .ToList();

        var lines = new List<IProductSalesLine>();
        foreach (var entry in byProduct)
        {
            var product = await _products.FindByIdAsync(entry.ProductId, ct);
            lines.Add(new ProductSalesLineView
            {
                ProductId = entry.ProductId,
                ProductName = product?.Name ?? "(unknown product)",
                QuantitySold = entry.Quantity,
                RevenueTotal = entry.Revenue
            });
        }

        return lines;
    }

    public async Task<IReadOnlyList<IStockOnHandLine>> GetStockOnHandAsync(Guid storeId, CancellationToken ct = default)
    {
        var onHand = await _stockLedger.GetOnHandByProductAsync(storeId, ct);

        var lines = new List<IStockOnHandLine>();
        foreach (var (productId, quantity) in onHand)
        {
            var product = await _products.FindByIdAsync(productId, ct);
            lines.Add(new StockOnHandLineView
            {
                ProductId = productId,
                ProductName = product?.Name ?? "(unknown product)",
                QuantityOnHand = quantity
            });
        }

        return lines.OrderBy(l => l.ProductName).ToList();
    }

    public async Task<IRegisterSessionReport?> GetRegisterSessionReportAsync(
        Guid registerSessionId, CancellationToken ct = default)
    {
        var session = await _registerSessions.FindByIdAsync(registerSessionId, ct);
        if (session is null) return null;

        var movements = await _cashMovements.GetForSessionAsync(registerSessionId, ct);

        var cashSalesTotal = movements.Where(m => m.Type == CashMovementType.CashSaleTender).Sum(m => m.Amount);
        var cashInTotal = movements.Where(m => m.Type == CashMovementType.CashIn).Sum(m => m.Amount);
        var cashOutTotal = movements.Where(m => m.Type == CashMovementType.CashOut).Sum(m => m.Amount);
        // CashRefundTender included in the live total for completeness even
        // though nothing generates one yet (Phase 6 note) — sums to 0 today,
        // costs nothing to include now, avoids a silent omission later.
        var cashRefundTotal = movements.Where(m => m.Type == CashMovementType.CashRefundTender).Sum(m => m.Amount);

        var liveExpectedCash = session.OpeningFloat + cashSalesTotal + cashInTotal + cashOutTotal + cashRefundTotal;

        return new RegisterSessionReportView
        {
            RegisterSessionId = session.Id,
            RegisterId = session.RegisterId,
            Status = session.Status.ToString(),
            OpenedAt = session.OpenedAt,
            ClosedAt = session.ClosedAt,
            OpeningFloat = session.OpeningFloat,
            CashSalesTotal = cashSalesTotal,
            CashInTotal = cashInTotal,
            CashOutTotal = cashOutTotal,
            LiveExpectedCash = liveExpectedCash,
            DeclaredClosingCash = session.DeclaredClosingCash,
            Discrepancy = session.Discrepancy
        };
    }

    private async Task<IReadOnlyList<Sale>> GetSalesForLocalDateAsync(Guid storeId, DateOnly date, CancellationToken ct)
    {
        var store = await _stores.FindByIdAsync(storeId, ct)
            ?? throw new InvalidOperationException($"Store {storeId} was not found.");

        // Store's local calendar day converted to a UTC range — same timezone
        // discipline as PosSaleService/QuoteService's fiscal timestamp handling.
        // A local midnight-to-midnight window, expressed in UTC, is what
        // FindByStoreAndDateRangeAsync filters Sale.CompletedAt against.
        var storeTimeZone = TimeZoneInfo.FindSystemTimeZoneById(store.TimeZoneId);
        var localMidnight = date.ToDateTime(TimeOnly.MinValue);
        var localMidnightNextDay = date.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localMidnight, DateTimeKind.Unspecified), storeTimeZone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localMidnightNextDay, DateTimeKind.Unspecified), storeTimeZone);

        return await _sales.FindByStoreAndDateRangeAsync(
            storeId, new DateTimeOffset(fromUtc, TimeSpan.Zero), new DateTimeOffset(toUtc, TimeSpan.Zero), ct);
    }
}
