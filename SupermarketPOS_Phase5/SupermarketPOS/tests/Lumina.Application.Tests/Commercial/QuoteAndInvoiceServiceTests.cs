using Lumina.Application.Commercial;
using Lumina.Application.Ports;
using Lumina.Contracts.Commercial;
using Lumina.Domain.Commercial;
using Lumina.Domain.Identity;
using Xunit;

namespace Lumina.Application.Tests.Commercial;

public class QuoteAndInvoiceServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid StoreId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private sealed class FakeQuoteRepository : IQuoteRepository
    {
        public Quote? Quote;
        public Task<Quote?> FindByIdAsync(Guid quoteId, CancellationToken ct = default) => Task.FromResult(Quote);
        public Task AddAsync(Quote quote, CancellationToken ct = default) { Quote = quote; return Task.CompletedTask; }
        public Task UpdateAsync(Quote quote, CancellationToken ct = default) { Quote = quote; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeInvoiceRepository : IInvoiceRepository
    {
        public readonly List<Invoice> Invoices = new();
        private int _counter;
        public Task<Invoice?> FindByIdAsync(Guid invoiceId, CancellationToken ct = default) =>
            Task.FromResult(Invoices.FirstOrDefault(i => i.Id == invoiceId));
        public Task<Invoice?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(Invoices.FirstOrDefault(i => i.IdempotencyKey == idempotencyKey));
        public Task AddAsync(Invoice invoice, CancellationToken ct = default) { Invoices.Add(invoice); return Task.CompletedTask; }
        public Task UpdateStatusAsync(Invoice invoice, CancellationToken ct = default) => Task.CompletedTask; // fake: mutations already visible via shared reference
        public Task<string> NextInvoiceNumberAsync(Guid storeId, CancellationToken ct = default) =>
            Task.FromResult($"FA-{++_counter:D6}");
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeStoreRepository : IStoreRepository
    {
        public Store? Store;
        public Task<Store?> FindByIdAsync(Guid storeId, CancellationToken ct = default) =>
            Task.FromResult(Store?.Id == storeId ? Store : null);
        public Task<bool> BelongsToTenantAsync(Guid storeId, Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Store?.Id == storeId && Store.TenantId == tenantId);
        public Task<Store?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Store?.TenantId == tenantId ? Store : null);
    }

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public Tenant? Tenant;
        public Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(Tenant?.Id == tenantId ? Tenant : null);
    }

    private sealed class FakeFiscalGenerator : IFiscalRecordGenerator
    {
        public int GenerateCallCount;
        public int CancelCallCount;
        public Task<FiscalRecordResult> GenerateAsync(Guid sifBoundaryId, FiscalRecordRequest request, CancellationToken ct = default)
        {
            GenerateCallCount++;
            return Task.FromResult(new FiscalRecordResult(Guid.NewGuid(), "fakehash", "https://example.test/qr"));
        }
        public Task<FiscalCancellationResult> GenerateCancellationAsync(Guid sifBoundaryId, FiscalCancellationRequest request, CancellationToken ct = default)
        {
            CancelCallCount++;
            return Task.FromResult(new FiscalCancellationResult(Guid.NewGuid(), "fakecancelhash"));
        }
    }

    private sealed class Fixture
    {
        public FakeQuoteRepository Quotes = new();
        public FakeInvoiceRepository Invoices = new();
        public FakeStoreRepository Stores = new();
        public FakeTenantRepository Tenants = new();
        public FakeFiscalGenerator Fiscal = new();

        public QuoteService BuildQuoteService() => new(Quotes, Invoices, Stores, Tenants, Fiscal);
        public InvoiceService BuildInvoiceService() => new(Invoices, Stores, Tenants, Fiscal);
    }

    private static Fixture SetUp()
    {
        var f = new Fixture();
        f.Stores.Store = new Store(StoreId, TenantId, "Main Store", "PerStore");
        f.Tenants.Tenant = new Tenant(TenantId, "Test Tenant SL", "B12345678");
        return f;
    }

    [Fact]
    public async Task CreateAsync_CreatesDraftQuote()
    {
        var f = SetUp();
        var svc = f.BuildQuoteService();

        var summary = await svc.CreateAsync(new CreateQuoteRequest(StoreId, CustomerId, DateTimeOffset.UtcNow.AddDays(30)));

        Assert.Equal("Draft", summary.Status);
        Assert.Equal(CustomerId, summary.CustomerId);
    }

    [Fact]
    public async Task FullFlow_DraftToInvoice_Succeeds()
    {
        var f = SetUp();
        var quoteSvc = f.BuildQuoteService();
        var summary = await quoteSvc.CreateAsync(new CreateQuoteRequest(StoreId, CustomerId, DateTimeOffset.UtcNow.AddDays(30)));

        await quoteSvc.AddLineAsync(summary.QuoteId, new AddQuoteLineRequest(Guid.NewGuid(), "Custom desk", 1, 250.00m, 0.21m));
        await quoteSvc.SendAsync(summary.QuoteId);
        await quoteSvc.AcceptAsync(summary.QuoteId);

        var result = await quoteSvc.ConvertToInvoiceAsync(summary.QuoteId, Guid.NewGuid());

        Assert.Equal(ConvertToInvoiceStatus.Success, result.Status);
        Assert.NotNull(result.InvoiceId);
        Assert.NotNull(result.InvoiceNumber);
        Assert.NotNull(result.QrPayload);
        Assert.Equal(1, f.Fiscal.GenerateCallCount);
        Assert.Equal(QuoteStatus.ConvertedToInvoice, f.Quotes.Quote!.Status);
    }

    [Fact]
    public async Task ConvertToInvoiceAsync_QuoteNotAccepted_ReturnsRejected()
    {
        var f = SetUp();
        var quoteSvc = f.BuildQuoteService();
        var summary = await quoteSvc.CreateAsync(new CreateQuoteRequest(StoreId, CustomerId, DateTimeOffset.UtcNow.AddDays(30)));
        await quoteSvc.AddLineAsync(summary.QuoteId, new AddQuoteLineRequest(Guid.NewGuid(), "Item", 1, 10m, 0.21m));
        // Never Sent/Accepted — still Draft

        var result = await quoteSvc.ConvertToInvoiceAsync(summary.QuoteId, Guid.NewGuid());

        Assert.Equal(ConvertToInvoiceStatus.Rejected, result.Status);
        Assert.Null(result.InvoiceId);
        Assert.Equal(0, f.Fiscal.GenerateCallCount);
    }

    [Fact]
    public async Task ConvertToInvoiceAsync_RetriedWithSameIdempotencyKey_DoesNotCallFiscalAgain()
    {
        var f = SetUp();
        var quoteSvc = f.BuildQuoteService();
        var summary = await quoteSvc.CreateAsync(new CreateQuoteRequest(StoreId, CustomerId, DateTimeOffset.UtcNow.AddDays(30)));
        await quoteSvc.AddLineAsync(summary.QuoteId, new AddQuoteLineRequest(Guid.NewGuid(), "Item", 1, 10m, 0.21m));
        await quoteSvc.SendAsync(summary.QuoteId);
        await quoteSvc.AcceptAsync(summary.QuoteId);
        var key = Guid.NewGuid();

        var first = await quoteSvc.ConvertToInvoiceAsync(summary.QuoteId, key);
        var retry = await quoteSvc.ConvertToInvoiceAsync(summary.QuoteId, key);

        Assert.Equal(first.InvoiceId, retry.InvoiceId);
        Assert.Equal(1, f.Fiscal.GenerateCallCount);
    }

    [Fact]
    public async Task CancelAsync_IssuedInvoice_Succeeds_AndGeneratesCancellationRecord()
    {
        var f = SetUp();
        var quoteSvc = f.BuildQuoteService();
        var summary = await quoteSvc.CreateAsync(new CreateQuoteRequest(StoreId, CustomerId, DateTimeOffset.UtcNow.AddDays(30)));
        await quoteSvc.AddLineAsync(summary.QuoteId, new AddQuoteLineRequest(Guid.NewGuid(), "Item", 1, 10m, 0.21m));
        await quoteSvc.SendAsync(summary.QuoteId);
        await quoteSvc.AcceptAsync(summary.QuoteId);
        var convertResult = await quoteSvc.ConvertToInvoiceAsync(summary.QuoteId, Guid.NewGuid());

        var invoiceSvc = f.BuildInvoiceService();
        var cancelResult = await invoiceSvc.CancelAsync(convertResult.InvoiceId!.Value, "Customer changed mind");

        Assert.Equal(CancelInvoiceStatus.Success, cancelResult.Status);
        Assert.Equal(1, f.Fiscal.CancelCallCount);
        var invoiceSummary = await invoiceSvc.FindByIdAsync(convertResult.InvoiceId.Value);
        Assert.Equal("Cancelled", invoiceSummary!.Status);
    }

    [Fact]
    public async Task CancelAsync_EmptyReason_ReturnsRejected_DoesNotCallFiscal()
    {
        var f = SetUp();
        f.Invoices.Invoices.Add(new Invoice(
            Guid.NewGuid(), StoreId, CustomerId, "FA-000001",
            new[] { new InvoiceLine(Guid.NewGuid(), "Item", 1, 10m, 0.21m, 10m, 2.1m, 12.1m) },
            null, Guid.NewGuid()));
        var invoiceSvc = f.BuildInvoiceService();

        var result = await invoiceSvc.CancelAsync(f.Invoices.Invoices[0].Id, "");

        Assert.Equal(CancelInvoiceStatus.Rejected, result.Status);
        Assert.Equal(0, f.Fiscal.CancelCallCount);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_ReturnsRejected()
    {
        var f = SetUp();
        var invoice = new Invoice(
            Guid.NewGuid(), StoreId, CustomerId, "FA-000001",
            new[] { new InvoiceLine(Guid.NewGuid(), "Item", 1, 10m, 0.21m, 10m, 2.1m, 12.1m) },
            null, Guid.NewGuid());
        invoice.Cancel(Guid.NewGuid());
        f.Invoices.Invoices.Add(invoice);
        var invoiceSvc = f.BuildInvoiceService();

        var result = await invoiceSvc.CancelAsync(invoice.Id, "Trying again");

        Assert.Equal(CancelInvoiceStatus.Rejected, result.Status);
        Assert.Equal(0, f.Fiscal.CancelCallCount);
    }
}
