using Lumina.Contracts.Stock;

namespace Lumina.Infrastructure.Stock;

/// <summary>
/// Temporary stand-in so the POS UI can compile and navigate to stock screens.
/// Replace with real ledger implementation in Phase 4 backend work — only swap DI.
/// </summary>
public sealed class StubStockService : IStockService
{
    public Task<StockBalanceResult> GetBalancesAsync(
        StockBalanceQuery query, CancellationToken ct = default)
    {
        return Task.FromResult(new StockBalanceResult(
            Array.Empty<IStockBalance>(), 0, query.Page, query.PageSize));
    }

    public Task<StockCommandResult> AdjustAsync(
        AdjustStockRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(new StockCommandResult(
            StockCommandStatus.Rejected,
            null,
            StockRejectionCode.Unknown,
            "Stock ledger not implemented yet (Phase 4 backend). UI is ready."));
    }

    public Task<StockCommandResult> CountAsync(
        CountStockRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(new StockCommandResult(
            StockCommandStatus.Rejected,
            null,
            StockRejectionCode.Unknown,
            "Stock ledger not implemented yet (Phase 4 backend). UI is ready."));
    }
}
