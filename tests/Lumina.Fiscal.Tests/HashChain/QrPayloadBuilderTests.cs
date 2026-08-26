using Lumina.Fiscal.HashChain;
using Xunit;

namespace Lumina.Fiscal.Tests.HashChain;

/// <summary>
/// These tests verify internal consistency (all fields present, correctly escaped,
/// stable format) — they do NOT verify compliance with the actual AEAT spec. See
/// the compliance warning in QrPayloadBuilder.cs. Do not treat a green test run
/// here as "QR is AEAT-compliant."
/// </summary>
public class QrPayloadBuilderTests
{
    private readonly QrPayloadBuilder _sut = new();

    [Fact]
    public void Build_IncludesAllRequiredFields()
    {
        var input = new VeriFactuRecordInput("B12345678", "FA-0001", new DateOnly(2026, 1, 15), 121.00m, "");

        var payload = _sut.Build(input);

        Assert.Contains("nif=B12345678", payload);
        Assert.Contains("numserie=FA-0001", payload);
        Assert.Contains("fecha=15-01-2026", payload);
        Assert.Contains("importe=121.00", payload);
    }

    [Fact]
    public void Build_EscapesSpecialCharactersInInvoiceNumber()
    {
        var input = new VeriFactuRecordInput("B12345678", "FA/2026 0001", new DateOnly(2026, 1, 15), 121.00m, "");

        var payload = _sut.Build(input);

        Assert.DoesNotContain(" ", payload);
        Assert.DoesNotContain("FA/2026 0001", payload); // raw unescaped form should not appear
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var input = new VeriFactuRecordInput("B12345678", "FA-0001", new DateOnly(2026, 1, 15), 121.00m, "");

        Assert.Equal(_sut.Build(input), _sut.Build(input));
    }
}
