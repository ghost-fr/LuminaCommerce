namespace Lumina.Contracts.Stock;

/// <summary>
/// Phase 4 stock contracts. UI builds screens against this; backend implements
/// the append-only stock ledger and registers <see cref="IStockService"/> in DI.
/// Until the ledger lands, UI may register a stub that returns empty/error.
/// </summary>

public interface IStockBalance
{
    Guid ProductId { get; }
    string Barcode { get; }
    string ProductName { get; }
    Guid StoreId { get; }
    decimal OnHand { get; }
    decimal Reserved { get; }
    decimal Available { get; }
}

public enum StockMovementReason
{
    Adjustment,
    CountCorrection,
    Sale,           // system-emitted from POS — not UI-driven
    TransferOut,
    TransferIn,
    Receipt
}

public record StockBalanceQuery(Guid? StoreId = null, string? BarcodeOrName = null, int Page = 1, int PageSize = 50);

public record StockBalanceResult(IReadOnlyList<IStockBalance> Items, int TotalCount, int Page, int PageSize);

/// <summary>Manual adjust (+/-). Requires stock.adjust capability.</summary>
public record AdjustStockRequest(
    Guid ProductId,
    Guid StoreId,
    decimal QuantityDelta,
    string ReasonNote,
    Guid IdempotencyKey);

/// <summary>Physical count sets absolute on-hand. Requires stock.adjust.</summary>
public record CountStockRequest(
    Guid ProductId,
    Guid StoreId,
    decimal CountedQuantity,
    string ReasonNote,
    Guid IdempotencyKey);

public enum StockCommandStatus
{
    Success,
    Rejected
}

public enum StockRejectionCode
{
    None,
    ProductNotFound,
    StoreNotFound,
    InsufficientStock,
    Unauthorized,
    DuplicateSubmission,
    Unknown
}

public record StockCommandResult(
    StockCommandStatus Status,
    IStockBalance? Balance,
    StockRejectionCode RejectionCode,
    string? RejectionReason);

public interface IStockService
{
    Task<StockBalanceResult> GetBalancesAsync(StockBalanceQuery query, CancellationToken ct = default);
    Task<StockCommandResult> AdjustAsync(AdjustStockRequest request, CancellationToken ct = default);
    Task<StockCommandResult> CountAsync(CountStockRequest request, CancellationToken ct = default);
}
