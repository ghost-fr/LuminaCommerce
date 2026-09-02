using Lumina.Domain.Cash;
using Xunit;

namespace Lumina.Domain.Tests.Cash;

public class CashMovementTests
{
    [Fact]
    public void Constructor_ZeroAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CashMovement(Guid.NewGuid(), Guid.NewGuid(), CashMovementType.CashIn, 0m, "test", null));
    }

    [Fact]
    public void Constructor_CashIn_WithoutReason_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CashMovement(Guid.NewGuid(), Guid.NewGuid(), CashMovementType.CashIn, 10m, null, null));
    }

    [Fact]
    public void Constructor_CashOut_PositiveAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CashMovement(Guid.NewGuid(), Guid.NewGuid(), CashMovementType.CashOut, 10m, "drop", null));
    }

    [Fact]
    public void Constructor_CashOut_NegativeAmount_Succeeds()
    {
        var movement = new CashMovement(Guid.NewGuid(), Guid.NewGuid(), CashMovementType.CashOut, -10m, "drop", null);
        Assert.Equal(-10m, movement.Amount);
    }

    [Fact]
    public void Constructor_CashIn_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CashMovement(Guid.NewGuid(), Guid.NewGuid(), CashMovementType.CashIn, -10m, "topup", null));
    }

    [Fact]
    public void Constructor_CashSaleTender_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CashMovement(Guid.NewGuid(), Guid.NewGuid(), CashMovementType.CashSaleTender, -5m, null, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_CashRefundTender_PositiveAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CashMovement(Guid.NewGuid(), Guid.NewGuid(), CashMovementType.CashRefundTender, 5m, null, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_CashSaleTender_NoReasonRequired()
    {
        var movement = new CashMovement(Guid.NewGuid(), Guid.NewGuid(), CashMovementType.CashSaleTender, 5m, null, Guid.NewGuid());
        Assert.Null(movement.Reason);
    }
}

public class RegisterSessionTests
{
    [Fact]
    public void Constructor_NegativeOpeningFloat_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RegisterSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -1m));
    }

    [Fact]
    public void Constructor_ValidOpeningFloat_StatusIsOpen()
    {
        var session = new RegisterSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m);
        Assert.Equal(RegisterSessionStatus.Open, session.Status);
    }

    [Fact]
    public void Close_ComputesDiscrepancy_Positive_WhenMoreCashThanExpected()
    {
        var session = new RegisterSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m);

        session.Close(Guid.NewGuid(), declaredClosingCash: 155m, expectedClosingCash: 150m);

        Assert.Equal(5m, session.Discrepancy);
        Assert.Equal(RegisterSessionStatus.Closed, session.Status);
    }

    [Fact]
    public void Close_ComputesDiscrepancy_Negative_WhenLessCashThanExpected()
    {
        var session = new RegisterSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m);

        session.Close(Guid.NewGuid(), declaredClosingCash: 140m, expectedClosingCash: 150m);

        Assert.Equal(-10m, session.Discrepancy);
    }

    [Fact]
    public void Close_ExactMatch_ZeroDiscrepancy()
    {
        var session = new RegisterSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m);

        session.Close(Guid.NewGuid(), declaredClosingCash: 150m, expectedClosingCash: 150m);

        Assert.Equal(0m, session.Discrepancy);
    }

    [Fact]
    public void Close_AlreadyClosed_Throws()
    {
        var session = new RegisterSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m);
        session.Close(Guid.NewGuid(), 100m, 100m);

        Assert.Throws<InvalidOperationException>(() => session.Close(Guid.NewGuid(), 100m, 100m));
    }

    [Fact]
    public void Close_NegativeDeclaredCash_Throws()
    {
        var session = new RegisterSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Close(Guid.NewGuid(), -1m, 100m));
    }
}
