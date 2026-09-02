namespace Lumina.Contracts.Reporting;

public record DailySalesReportRequest(Guid StoreId, DateOnly Date);

/// <summary>
/// Every total here is computed LIVE from the underlying Sale records at
/// request time — never a separately-tracked or cached counter. This is
/// deliberate: the whole point of this contract is that a daily report can
/// never drift from the raw sale data, by construction, not by discipline.
/// </summary>
public interface IDailySalesReport
{
    Guid StoreId { get; }
    DateOnly Date { get; }
    int SaleCount { get; }
    decimal GrossSubtotal { get; }
    decimal VatTotal { get; }
    decimal GrandTotal { get; }
    decimal CashTotal { get; }
    decimal CardTotal { get; }
    decimal OtherTenderTotal { get; }
}

public record TopProductsRequest(Guid StoreId, DateOnly Date, int Limit = 10);

public interface IProductSalesLine
{
    Guid ProductId { get; }
    string ProductName { get; }
    decimal QuantitySold { get; }
    decimal RevenueTotal { get; }
}

public interface IStockOnHandLine
{
    Guid ProductId { get; }
    string ProductName { get; }
    decimal QuantityOnHand { get; }
}

/// <summary>
/// Live reconciliation view of a register session — usable for BOTH an
/// in-progress session ("X-report," a mid-shift check) and a closed one
/// ("Z-report"). CashSalesTotal/CashInTotal/CashOutTotal are always computed
/// live from CashMovements; DeclaredClosingCash/Discrepancy are null until the
/// session is actually closed (they're operator-reported values captured at
/// close time, not derivable before then).
/// </summary>
public interface IRegisterSessionReport
{
    Guid RegisterSessionId { get; }
    Guid RegisterId { get; }
    string Status { get; }
    DateTimeOffset OpenedAt { get; }
    DateTimeOffset? ClosedAt { get; }
    decimal OpeningFloat { get; }
    decimal CashSalesTotal { get; }
    decimal CashInTotal { get; }
    decimal CashOutTotal { get; }

    /// <summary>OpeningFloat + CashSalesTotal + CashInTotal + CashOutTotal,
    /// computed live — for an OPEN session this is "what should be in the
    /// drawer right now"; for a CLOSED session this equals the persisted
    /// RegisterSession.ExpectedClosingCash exactly (same formula, same
    /// inputs) — never a second, independently-drifting calculation.</summary>
    decimal LiveExpectedCash { get; }

    decimal? DeclaredClosingCash { get; }
    decimal? Discrepancy { get; }
}

/// <summary>
/// UI-facing contract for reporting/dashboard screens. Concrete implementation
/// is Lumina.Application.Reporting.ReportingService.
///
/// UI (Grok) may assume:
/// - All figures are computed live on every call — there's no "refresh" or
///   staleness concept to worry about, but also no caching, so avoid calling
///   these in a tight loop for a large date range.
/// - GetDailySalesReportAsync uses the STORE'S LOCAL calendar day (via
///   Store.TimeZoneId), not a raw UTC day — same timezone handling as the
///   fiscal record generation in IPosSaleService/IQuoteService.
/// </summary>
public interface IReportingService
{
    Task<IDailySalesReport> GetDailySalesReportAsync(DailySalesReportRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<IProductSalesLine>> GetTopProductsAsync(TopProductsRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<IStockOnHandLine>> GetStockOnHandAsync(Guid storeId, CancellationToken ct = default);
    Task<IRegisterSessionReport?> GetRegisterSessionReportAsync(Guid registerSessionId, CancellationToken ct = default);
}
