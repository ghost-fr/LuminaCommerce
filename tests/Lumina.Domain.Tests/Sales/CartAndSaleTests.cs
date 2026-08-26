using Lumina.Domain.Sales;
using Xunit;

namespace Lumina.Domain.Tests.Sales;

public class CartTests
{
    [Fact]
    public void AddLine_NewProduct_CreatesLine()
    {
        var cart = new Cart(Guid.NewGuid(), Guid.NewGuid());
        var productId = Guid.NewGuid();

        cart.AddLine(productId, 2, 10.00m, 0.21m, null);

        Assert.Single(cart.Lines);
        Assert.Equal(20.00m, cart.Subtotal);
        Assert.Equal(4.20m, cart.VatTotal);
        Assert.Equal(24.20m, cart.Total);
    }

    [Fact]
    public void AddLine_SameProductSamePrice_IncrementsExistingLine()
    {
        var cart = new Cart(Guid.NewGuid(), Guid.NewGuid());
        var productId = Guid.NewGuid();

        cart.AddLine(productId, 1, 10.00m, 0.21m, null);
        cart.AddLine(productId, 1, 10.00m, 0.21m, null);

        Assert.Single(cart.Lines);
        Assert.Equal(2, cart.Lines[0].Quantity);
    }

    [Fact]
    public void AddLine_SameProductDifferentPrice_CreatesSeparateLine()
    {
        var cart = new Cart(Guid.NewGuid(), Guid.NewGuid());
        var productId = Guid.NewGuid();

        cart.AddLine(productId, 1, 10.00m, 0.21m, null);
        cart.AddLine(productId, 1, 9.00m, 0.21m, "PROMO");

        Assert.Equal(2, cart.Lines.Count);
    }

    [Fact]
    public void AddLine_ZeroOrNegativeQuantity_Throws()
    {
        var cart = new Cart(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<ArgumentOutOfRangeException>(() => cart.AddLine(Guid.NewGuid(), 0, 10.00m, 0.21m, null));
    }

    [Fact]
    public void RemoveQuantity_PartialRemoval_KeepsLine()
    {
        var cart = new Cart(Guid.NewGuid(), Guid.NewGuid());
        var productId = Guid.NewGuid();
        cart.AddLine(productId, 3, 10.00m, 0.21m, null);

        cart.RemoveQuantity(productId, 1);

        Assert.Single(cart.Lines);
        Assert.Equal(2, cart.Lines[0].Quantity);
    }

    [Fact]
    public void RemoveQuantity_FullRemoval_RemovesLine()
    {
        var cart = new Cart(Guid.NewGuid(), Guid.NewGuid());
        var productId = Guid.NewGuid();
        cart.AddLine(productId, 1, 10.00m, 0.21m, null);

        cart.RemoveQuantity(productId, 1);

        Assert.Empty(cart.Lines);
    }

    [Fact]
    public void RemoveQuantity_UnknownProduct_Throws()
    {
        var cart = new Cart(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => cart.RemoveQuantity(Guid.NewGuid(), 1));
    }
}

public class SaleTests
{
    private static SaleLine Line(decimal subtotal, decimal vat) =>
        new(Guid.NewGuid(), 1, subtotal, 0.21m, subtotal, vat, subtotal + vat, null);

    [Fact]
    public void Constructor_TenderMatchesTotal_Succeeds()
    {
        var line = Line(10.00m, 2.10m);
        var sale = new Sale(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "T-000001",
            new[] { line }, new[] { new SaleTender("Cash", 12.10m, null) }, Guid.NewGuid());

        Assert.Equal(12.10m, sale.Total);
    }

    [Fact]
    public void Constructor_TenderMismatch_Throws()
    {
        var line = Line(10.00m, 2.10m);
        Assert.Throws<InvalidOperationException>(() => new Sale(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "T-000001",
            new[] { line }, new[] { new SaleTender("Cash", 5.00m, null) }, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_NoLines_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Sale(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "T-000001",
            Array.Empty<SaleLine>(), new[] { new SaleTender("Cash", 0m, null) }, Guid.NewGuid()));
    }

    [Fact]
    public void AttachVeriFactuRecord_SecondCall_Throws()
    {
        var line = Line(10.00m, 2.10m);
        var sale = new Sale(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "T-000001",
            new[] { line }, new[] { new SaleTender("Cash", 12.10m, null) }, Guid.NewGuid());

        sale.AttachVeriFactuRecord(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => sale.AttachVeriFactuRecord(Guid.NewGuid()));
    }
}

public class RegisterTests
{
    [Fact]
    public void Open_SetsIsOpenTrue()
    {
        var register = new Register(Guid.NewGuid(), Guid.NewGuid(), "Register 1");
        register.Open();
        Assert.True(register.IsOpen);
    }

    [Fact]
    public void Close_SetsIsOpenFalse()
    {
        var register = new Register(Guid.NewGuid(), Guid.NewGuid(), "Register 1");
        register.Open();
        register.Close();
        Assert.False(register.IsOpen);
    }
}
