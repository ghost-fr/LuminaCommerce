using Lumina.Application.Ports;
using Lumina.Fiscal.HashChain;
using Xunit;

namespace Lumina.Fiscal.Tests.HashChain;

public class FiscalRecordGeneratorChainIntegrityTests
{
    private sealed class FakeChainStore : IVeriFactuChainStore
    {
        // In-memory chain, in insertion order — mirrors what a real DB would
        // store now that AppendAsync captures PreviousHash/GeneratedAt too.
        public readonly List<ChainLink> Links = new();

        public Task<string> GetLastHashAsync(Guid sifBoundaryId, CancellationToken ct = default) =>
            Task.FromResult(Links.Count == 0 ? "" : Links[^1].RecordHash);

        public Task<IReadOnlyList<ChainLink>> GetRecentLinksAsync(Guid sifBoundaryId, int count, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ChainLink>>(Links.AsEnumerable().Reverse().Take(count).ToList());

        public Task AppendAsync(
            Guid sifBoundaryId, Guid recordId, string recordHash, string previousHash, DateTimeOffset generatedAt,
            CancellationToken ct = default)
        {
            Links.Add(new ChainLink(recordHash, previousHash, generatedAt));
            return Task.CompletedTask;
        }
    }

    private static FiscalRecordGenerator BuildSut(FakeChainStore store) =>
        new(new HashChainService(), new QrPayloadBuilder(), store);

    private static FiscalRecordRequest MakeRequest(DateTimeOffset generatedAt) =>
        new("B12345678", "T-000001", DateOnly.FromDateTime(generatedAt.Date), "F2", 2.10m, 12.10m, generatedAt);

    [Fact]
    public async Task GenerateAsync_FirstRecordEver_NoIntegrityCheckNeeded_Succeeds()
    {
        var store = new FakeChainStore();
        var sut = BuildSut(store);

        var result = await sut.GenerateAsync(Guid.NewGuid(), MakeRequest(DateTimeOffset.Now));

        Assert.NotNull(result.RecordHash);
        Assert.Single(store.Links);
    }

    [Fact]
    public async Task GenerateAsync_SecondRecord_ChainIntact_Succeeds()
    {
        var store = new FakeChainStore();
        var sut = BuildSut(store);
        var boundary = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

        await sut.GenerateAsync(boundary, MakeRequest(t0));
        var second = await sut.GenerateAsync(boundary, MakeRequest(t0.AddMinutes(5)));

        Assert.NotNull(second.RecordHash);
        Assert.Equal(2, store.Links.Count);
    }

    [Fact]
    public async Task GenerateAsync_CorruptedChain_ThrowsFiscalChainIntegrityException()
    {
        var store = new FakeChainStore();
        var sut = BuildSut(store);
        var boundary = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

        await sut.GenerateAsync(boundary, MakeRequest(t0));
        await sut.GenerateAsync(boundary, MakeRequest(t0.AddMinutes(5)));

        // Simulate tampering: directly corrupt the last link's stored
        // PreviousHash so it no longer matches the second-to-last record's
        // actual hash — exactly the scenario art. 7.i's pre-check exists to catch.
        var lastIndex = store.Links.Count - 1;
        var tampered = store.Links[lastIndex] with { PreviousHash = "0000000000000000000000000000000000000000000000000000000000000" };
        store.Links[lastIndex] = tampered;

        await Assert.ThrowsAsync<FiscalChainIntegrityException>(() =>
            sut.GenerateAsync(boundary, MakeRequest(t0.AddMinutes(10))));
    }

    [Fact]
    public async Task GenerateAsync_PreviousTimestampMoreThanOneMinuteAhead_ThrowsFiscalChainIntegrityException()
    {
        var store = new FakeChainStore();
        var sut = BuildSut(store);
        var boundary = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 1, 1, 10, 5, 0, TimeSpan.Zero);

        await sut.GenerateAsync(boundary, MakeRequest(t0));

        // New record's timestamp is more than a minute BEHIND the previous
        // one — e.g. a clock rollback or an out-of-order write.
        var earlierTimestamp = t0.AddMinutes(-2);

        await Assert.ThrowsAsync<FiscalChainIntegrityException>(() =>
            sut.GenerateAsync(boundary, MakeRequest(earlierTimestamp)));
    }

    [Fact]
    public async Task GenerateAsync_WithinOneMinuteTolerance_Succeeds()
    {
        var store = new FakeChainStore();
        var sut = BuildSut(store);
        var boundary = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 1, 1, 10, 5, 0, TimeSpan.Zero);

        await sut.GenerateAsync(boundary, MakeRequest(t0));

        // 30 seconds "behind" is within the 1-minute tolerance — should succeed.
        var withinTolerance = t0.AddSeconds(-30);

        var result = await sut.GenerateAsync(boundary, MakeRequest(withinTolerance));
        Assert.NotNull(result.RecordHash);
    }

    [Fact]
    public async Task GenerateCancellationAsync_AlsoRunsChainIntegrityCheck()
    {
        var store = new FakeChainStore();
        var sut = BuildSut(store);
        var boundary = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

        await sut.GenerateAsync(boundary, MakeRequest(t0));
        await sut.GenerateAsync(boundary, MakeRequest(t0.AddMinutes(1)));

        var lastIndex = store.Links.Count - 1;
        store.Links[lastIndex] = store.Links[lastIndex] with { PreviousHash = "corrupted" };

        await Assert.ThrowsAsync<FiscalChainIntegrityException>(() =>
            sut.GenerateCancellationAsync(boundary,
                new FiscalCancellationRequest("B12345678", "T-000001", DateOnly.FromDateTime(t0.Date), t0.AddMinutes(2))));
    }
}
