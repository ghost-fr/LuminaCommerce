using Lumina.Fiscal.HashChain;
using Xunit;

namespace Lumina.Fiscal.Tests.HashChain;

/// <summary>
/// Fixed-vector tests: hashes below were computed independently (Python hashlib,
/// not this codebase) against the documented canonical format in
/// HashChainService.cs. If these ever fail after a code change, that change altered
/// the canonical format or hash algorithm — treat as a breaking change requiring a
/// new SIF boundary, never "fix the test to match the new hash."
/// </summary>
public class HashChainServiceTests
{
    private readonly HashChainService _sut = new();

    private const string FirstRecordExpectedHash =
        "caf994fe994d86f26a2d41f8e10d5ef7b6a69f9a897b1043064193ff23766ee7";
    // canonical input: "B12345678|FA-0001|2026-01-15|121.00|"

    private const string SecondRecordExpectedHash =
        "251e7b966ab9c079fca49b2fbce5ddc9a86bf823cc1184b4a82ae4bfe54c7eb9";
    // canonical input: "B12345678|FA-0002|2026-01-16|60.50|" + FirstRecordExpectedHash

    [Fact]
    public void ComputeHash_FirstRecordInChain_MatchesFixedVector()
    {
        var input = new VeriFactuRecordInput(
            IssuerNif: "B12345678",
            InvoiceSeriesAndNumber: "FA-0001",
            IssueDate: new DateOnly(2026, 1, 15),
            TotalAmount: 121.00m,
            PreviousRecordHash: "");

        var hash = _sut.ComputeHash(input);

        Assert.Equal(FirstRecordExpectedHash, hash);
    }

    [Fact]
    public void ComputeHash_SecondRecordInChain_MatchesFixedVector()
    {
        var input = new VeriFactuRecordInput(
            IssuerNif: "B12345678",
            InvoiceSeriesAndNumber: "FA-0002",
            IssueDate: new DateOnly(2026, 1, 16),
            TotalAmount: 60.50m,
            PreviousRecordHash: FirstRecordExpectedHash);

        var hash = _sut.ComputeHash(input);

        Assert.Equal(SecondRecordExpectedHash, hash);
    }

    [Fact]
    public void ComputeHash_IsDeterministic_SameInputSameHash()
    {
        var input = new VeriFactuRecordInput("B12345678", "FA-0001", new DateOnly(2026, 1, 15), 121.00m, "");

        var hash1 = _sut.ComputeHash(input);
        var hash2 = _sut.ComputeHash(input);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentAmount_ProducesDifferentHash()
    {
        var a = new VeriFactuRecordInput("B12345678", "FA-0001", new DateOnly(2026, 1, 15), 121.00m, "");
        var b = new VeriFactuRecordInput("B12345678", "FA-0001", new DateOnly(2026, 1, 15), 121.01m, "");

        Assert.NotEqual(_sut.ComputeHash(a), _sut.ComputeHash(b));
    }

    [Fact]
    public void ComputeHash_TamperedPreviousHash_ProducesDifferentHash()
    {
        var genuine = new VeriFactuRecordInput("B12345678", "FA-0002", new DateOnly(2026, 1, 16), 60.50m,
            FirstRecordExpectedHash);
        var tampered = genuine with
        {
            PreviousRecordHash = "0000000000000000000000000000000000000000000000000000000000000"
        };

        Assert.NotEqual(_sut.ComputeHash(genuine), _sut.ComputeHash(tampered));
    }

    [Fact]
    public void CreateRecord_SetsRecordHashCorrectly()
    {
        var input = new VeriFactuRecordInput("B12345678", "FA-0001", new DateOnly(2026, 1, 15), 121.00m, "");
        var sifBoundaryId = Guid.NewGuid();

        var record = _sut.CreateRecord(Guid.NewGuid(), sifBoundaryId, input);

        Assert.Equal(FirstRecordExpectedHash, record.RecordHash);
        Assert.Equal(sifBoundaryId, record.SifBoundaryId);
    }
}
