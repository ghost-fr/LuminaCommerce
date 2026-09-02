using Lumina.Domain.Commercial;
using Xunit;

namespace Lumina.Domain.Tests.Commercial;

public class QuoteTests
{
    private static Quote NewDraftQuote() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(30));

    [Fact]
    public void Constructor_EmptyCustomerId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Quote(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTimeOffset.UtcNow.AddDays(30)));
    }

    [Fact]
    public void AddLine_WhileDraft_Succeeds()
    {
        var quote = NewDraftQuote();
        quote.AddLine(Guid.NewGuid(), "Custom widget", 2, 10.00m, 0.21m);
        Assert.Single(quote.Lines);
        Assert.Equal(24.20m, quote.Total);
    }

    [Fact]
    public void AddLine_AfterSend_Throws()
    {
        var quote = NewDraftQuote();
        quote.AddLine(Guid.NewGuid(), "Widget", 1, 10.00m, 0.21m);
        quote.Send();

        Assert.Throws<InvalidOperationException>(() => quote.AddLine(Guid.NewGuid(), "Another", 1, 5m, 0.21m));
    }

    [Fact]
    public void Send_EmptyQuote_Throws()
    {
        var quote = NewDraftQuote();
        Assert.Throws<InvalidOperationException>(() => quote.Send());
    }

    [Fact]
    public void FullHappyPath_DraftToAccepted()
    {
        var quote = NewDraftQuote();
        quote.AddLine(Guid.NewGuid(), "Widget", 1, 10.00m, 0.21m);
        quote.Send();
        quote.Accept();
        Assert.Equal(QuoteStatus.Accepted, quote.Status);
    }

    [Fact]
    public void Accept_WithoutSendFirst_Throws()
    {
        var quote = NewDraftQuote();
        quote.AddLine(Guid.NewGuid(), "Widget", 1, 10.00m, 0.21m);
        Assert.Throws<InvalidOperationException>(() => quote.Accept());
    }

    [Fact]
    public void Reject_FromSent_Succeeds()
    {
        var quote = NewDraftQuote();
        quote.AddLine(Guid.NewGuid(), "Widget", 1, 10.00m, 0.21m);
        quote.Send();
        quote.Reject();
        Assert.Equal(QuoteStatus.Rejected, quote.Status);
    }

    [Fact]
    public void MarkConverted_OnlyFromAccepted_Succeeds()
    {
        var quote = NewDraftQuote();
        quote.AddLine(Guid.NewGuid(), "Widget", 1, 10.00m, 0.21m);
        quote.Send();
        quote.Accept();

        var invoiceId = Guid.NewGuid();
        quote.MarkConverted(invoiceId);

        Assert.Equal(QuoteStatus.ConvertedToInvoice, quote.Status);
        Assert.Equal(invoiceId, quote.ConvertedInvoiceId);
    }

    [Fact]
    public void MarkConverted_WithoutAccepted_Throws()
    {
        var quote = NewDraftQuote();
        quote.AddLine(Guid.NewGuid(), "Widget", 1, 10.00m, 0.21m);
        quote.Send();

        Assert.Throws<InvalidOperationException>(() => quote.MarkConverted(Guid.NewGuid()));
    }

    [Fact]
    public void RemoveLine_WhileDraft_Succeeds()
    {
        var quote = NewDraftQuote();
        var productId = Guid.NewGuid();
        quote.AddLine(productId, "Widget", 1, 10.00m, 0.21m);

        quote.RemoveLine(productId);

        Assert.Empty(quote.Lines);
    }
}

public class InvoiceTests
{
    private static InvoiceLine Line(decimal subtotal, decimal vat) =>
        new(Guid.NewGuid(), "Widget", 1, subtotal, 0.21m, subtotal, vat, subtotal + vat);

    private static Invoice NewIssuedInvoice() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "FA-000001",
            new[] { Line(10.00m, 2.10m) }, sourceQuoteId: Guid.NewGuid(), idempotencyKey: Guid.NewGuid());

    [Fact]
    public void Constructor_EmptyCustomerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Invoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "FA-000001",
            new[] { Line(10.00m, 2.10m) }, null, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_NoLines_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Invoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "FA-000001",
            Array.Empty<InvoiceLine>(), null, Guid.NewGuid()));
    }

    [Fact]
    public void Cancel_FromIssued_Succeeds()
    {
        var invoice = NewIssuedInvoice();
        var cancellationRecordId = Guid.NewGuid();

        invoice.Cancel(cancellationRecordId);

        Assert.Equal(InvoiceStatus.Cancelled, invoice.Status);
        Assert.Equal(cancellationRecordId, invoice.CancellationVeriFactuRecordId);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        var invoice = NewIssuedInvoice();
        invoice.Cancel(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => invoice.Cancel(Guid.NewGuid()));
    }

    [Fact]
    public void AttachVeriFactuRecord_SecondCall_Throws()
    {
        var invoice = NewIssuedInvoice();
        invoice.AttachVeriFactuRecord(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => invoice.AttachVeriFactuRecord(Guid.NewGuid()));
    }

    [Fact]
    public void MarkRectified_FromIssued_Succeeds()
    {
        var invoice = NewIssuedInvoice();
        invoice.MarkRectified();
        Assert.Equal(InvoiceStatus.Rectified, invoice.Status);
    }
}
