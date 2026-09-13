using Lumina.Fiscal.HashChain;
using Xunit;

namespace Lumina.Fiscal.Tests.HashChain;

/// <summary>
/// AEAT's own Java example: codificarQR("https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR?",
/// "89890001K", "12345678&amp;G33", "01-01-2024", "241.40"). The numserie value was
/// deliberately chosen by AEAT to contain a literal "&amp;" — proving per-parameter
/// encoding, not naive string concatenation, is required. This is a real fixed
/// vector, not a self-invented one.
/// </summary>
public class QrPayloadBuilderTests
{
    private readonly QrPayloadBuilder _sut = new();

    [Fact]
    public void Build_AeatJavaExample_MatchesExpectedStructure()
    {
        var input = new RegistrationRecordInput(
            IssuerNif: "89890001K",
            InvoiceSeriesAndNumber: "12345678&G33",
            IssueDate: new DateOnly(2024, 1, 1),
            InvoiceType: "F1",
            VatAmount: 0m,
            TotalAmount: 241.40m,
            PreviousHash: "",
            GeneratedAt: DateTimeOffset.Now);

        var payload = _sut.Build(input);

        Assert.Equal(
            "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR?" +
            "nif=89890001K&numserie=12345678%26G33&fecha=01-01-2024&importe=241.40",
            payload);
    }

    [Fact]
    public void Build_AmpersandInInvoiceNumber_IsProperlyEscaped_NotBreakingUrlStructure()
    {
        var input = new RegistrationRecordInput(
            "89890001K", "12345678&G33", new DateOnly(2024, 1, 1), "F1", 0m, 241.40m, "", DateTimeOffset.Now);

        var payload = _sut.Build(input);

        // exactly 4 "&" separators expected (one before each of numserie/fecha/importe);
        // an unescaped literal & inside numserie would produce a 5th, corrupting the query string
        Assert.Equal(3, payload.Count(c => c == '&'));
    }

    [Fact]
    public void Build_IncludesAllRequiredFields()
    {
        var input = new RegistrationRecordInput(
            "B12345678", "FA-0001", new DateOnly(2026, 1, 15), "F2", 10.00m, 121.00m, "", DateTimeOffset.Now);

        var payload = _sut.Build(input);

        Assert.Contains("nif=B12345678", payload);
        Assert.Contains("numserie=FA-0001", payload);
        Assert.Contains("fecha=15-01-2026", payload);
        Assert.Contains("importe=121.00", payload);
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var input = new RegistrationRecordInput(
            "B12345678", "FA-0001", new DateOnly(2026, 1, 15), "F2", 10.00m, 121.00m, "", DateTimeOffset.Now);

        Assert.Equal(_sut.Build(input), _sut.Build(input));
    }
}
