using Lumina.Domain.Stock;
using Xunit;

namespace Lumina.Domain.Tests.Stock;

public class StockMovementTests
{
    [Fact]
    public void Constructor_ZeroQuantity_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StockMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m, StockMovementReason.Receiving, null));
    }

    [Fact]
    public void Constructor_SaleReasonWithoutReferenceId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StockMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -1m, StockMovementReason.Sale, null));
    }

    [Fact]
    public void Constructor_SaleReasonWithReferenceId_Succeeds()
    {
        var saleId = Guid.NewGuid();
        var movement = new StockMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -1m, StockMovementReason.Sale, saleId);
        Assert.Equal(saleId, movement.ReferenceId);
    }

    [Fact]
    public void Constructor_PositiveDelta_Succeeds()
    {
        var movement = new StockMovement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, StockMovementReason.Receiving, null);
        Assert.Equal(10m, movement.QuantityDelta);
    }
}
