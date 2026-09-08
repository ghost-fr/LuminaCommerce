using Lumina.Fiscal.HashChain;
using Xunit;

namespace Lumina.Fiscal.Tests.HashChain;

/// <summary>
/// Fixed-vector tests using AEAT's OWN documented worked examples from "Detalle
/// de las especificaciones técnicas del código QR... Algoritmo de cálculo de
/// codificación de la huella" — Caso 2 (registration) and Caso 3 (cancellation).
/// These are not self-computed guesses: the input strings and expected hash
/// outputs below are copied verbatim from AEAT's own document, then independently
/// verified via Python's hashlib during development (see conversation record) —
/// both matched exactly. This is the strongest verification level available
/// short of a live AEAT sandbox round-trip.
///
/// If these tests ever fail after a code change, that change broke compliance
/// with AEAT's documented algorithm — never "fix the test," fix the code.
/// </summary>
public class HashChainServiceTests
{
    private readonly HashChainService _sut = new();

    // AEAT document "Caso 2: registro de facturación –en este caso, de alta– con
    // registro de facturación anterior existente en el SIF"
    [Fact]
    public void ComputeRegistrationHash_AeatCaso2_MatchesDocumentedOutput()
    {
        var input = new RegistrationRecordInput(
            IssuerNif: "89890001K",
            InvoiceSeriesAndNumber: "12345679/G34",
            IssueDate: new DateOnly(2024, 1, 1),
            InvoiceType: "F1",
            VatAmount: 12.35m,
            TotalAmount: 123.45m,
            PreviousHash: "3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60",
            GeneratedAt: new DateTimeOffset(2024, 1, 1, 19, 20, 35, TimeSpan.FromHours(1)));

        var hash = _sut.ComputeRegistrationHash(input);

        Assert.Equal("F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97", hash);
    }

    [Fact]
    public void BuildRegistrationCanonicalString_AeatCaso2_MatchesDocumentedString()
    {
        var input = new RegistrationRecordInput(
            "89890001K", "12345679/G34", new DateOnly(2024, 1, 1), "F1", 12.35m, 123.45m,
            "3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60",
            new DateTimeOffset(2024, 1, 1, 19, 20, 35, TimeSpan.FromHours(1)));

        var canonical = HashChainService.BuildRegistrationCanonicalString(input);

        Assert.Equal(
            "IDEmisorFactura=89890001K&NumSerieFactura=12345679/G34&FechaExpedicionFactura=01-01-2024" +
            "&TipoFactura=F1&CuotaTotal=12.35&ImporteTotal=123.45" +
            "&Huella=3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60" +
            "&FechaHoraHusoGenRegistro=2024-01-01T19:20:35+01:00",
            canonical);
    }

    // AEAT document "Caso 3: registro de facturación –en este caso, de anulación–
    // con registro de facturación anterior existente en el SIF"
    [Fact]
    public void ComputeCancellationHash_AeatCaso3_MatchesDocumentedOutput()
    {
        var input = new CancellationRecordInput(
            IssuerNif: "89890001K",
            InvoiceSeriesAndNumber: "12345679/G34",
            IssueDate: new DateOnly(2024, 1, 1),
            PreviousHash: "F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97",
            GeneratedAt: new DateTimeOffset(2024, 1, 1, 19, 20, 40, TimeSpan.FromHours(1)));

        var hash = _sut.ComputeCancellationHash(input);

        Assert.Equal("177547C0D57AC74748561D054A9CEC14B4C4EA23D1BEFD6F2E69E3A388F90C68", hash);
    }

    /// <summary>
    /// Bonus check beyond what AEAT's document states explicitly: their Caso 2
    /// and Caso 3 examples are actually a real consecutive pair — Caso 2's output
    /// hash is exactly the PreviousHash used as input to Caso 3. This confirms
    /// the chain-linking behavior end-to-end using AEAT's own data, not just each
    /// record type in isolation.
    /// </summary>
    [Fact]
    public void AeatCaso2And3_FormARealChain()
    {
        var registration = new RegistrationRecordInput(
            "89890001K", "12345679/G34", new DateOnly(2024, 1, 1), "F1", 12.35m, 123.45m,
            "3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60",
            new DateTimeOffset(2024, 1, 1, 19, 20, 35, TimeSpan.FromHours(1)));
        var registrationHash = _sut.ComputeRegistrationHash(registration);

        var cancellation = new CancellationRecordInput(
            "89890001K", "12345679/G34", new DateOnly(2024, 1, 1),
            PreviousHash: registrationHash, // chained from the record above, not hardcoded
            new DateTimeOffset(2024, 1, 1, 19, 20, 40, TimeSpan.FromHours(1)));
        var cancellationHash = _sut.ComputeCancellationHash(cancellation);

        Assert.Equal("177547C0D57AC74748561D054A9CEC14B4C4EA23D1BEFD6F2E69E3A388F90C68", cancellationHash);
    }

    // AEAT document "Caso 1: primer registro de facturación –en este caso, de
    // alta– en un Sistema Informático de Facturación (SIF)" — the first record
    // in a chain, where Huella is empty. Confirms BuildQueryString produces
    // "Huella=" (field name + "=" + nothing) rather than omitting the field
    // entirely or erroring on an empty value.
    [Fact]
    public void ComputeRegistrationHash_AeatCaso1_FirstRecordEmptyHuella_MatchesDocumentedOutput()
    {
        var input = new RegistrationRecordInput(
            IssuerNif: "89890001K",
            InvoiceSeriesAndNumber: "12345678/G33",
            IssueDate: new DateOnly(2024, 1, 1),
            InvoiceType: "F1",
            VatAmount: 12.35m,
            TotalAmount: 123.45m,
            PreviousHash: "",
            GeneratedAt: new DateTimeOffset(2024, 1, 1, 19, 20, 30, TimeSpan.FromHours(1)));

        var hash = _sut.ComputeRegistrationHash(input);

        Assert.Equal("3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60", hash);
    }

    [Fact]
    public void BuildRegistrationCanonicalString_EmptyPreviousHash_ProducesFieldNameWithEmptyValue()
    {
        var input = new RegistrationRecordInput(
            "89890001K", "12345678/G33", new DateOnly(2024, 1, 1), "F1", 12.35m, 123.45m, "",
            new DateTimeOffset(2024, 1, 1, 19, 20, 30, TimeSpan.FromHours(1)));

        var canonical = HashChainService.BuildRegistrationCanonicalString(input);

        Assert.Contains("&Huella=&FechaHoraHusoGenRegistro=", canonical);
    }

    /// <summary>
    /// The strongest check in this file: AEAT's three worked examples (Caso 1, 2,
    /// 3) are a genuine end-to-end chain — Caso 1's output feeds Caso 2's input,
    /// Caso 2's output feeds Caso 3's input. Reproducing all three from scratch
    /// and getting AEAT's exact documented outputs at every link is about as
    /// strong a verification as is possible without a live AEAT sandbox call.
    /// </summary>
    [Fact]
    public void AeatCaso1Through3_FormACompleteVerifiedChain()
    {
        var first = new RegistrationRecordInput(
            "89890001K", "12345678/G33", new DateOnly(2024, 1, 1), "F1", 12.35m, 123.45m, "",
            new DateTimeOffset(2024, 1, 1, 19, 20, 30, TimeSpan.FromHours(1)));
        var firstHash = _sut.ComputeRegistrationHash(first);
        Assert.Equal("3C464DAF61ACB827C65FDA19F352A4E3BDC2C640E9E9FC4CC058073F38F12F60", firstHash);

        var second = new RegistrationRecordInput(
            "89890001K", "12345679/G34", new DateOnly(2024, 1, 1), "F1", 12.35m, 123.45m,
            firstHash, new DateTimeOffset(2024, 1, 1, 19, 20, 35, TimeSpan.FromHours(1)));
        var secondHash = _sut.ComputeRegistrationHash(second);
        Assert.Equal("F7B94CFD8924EDFF273501B01EE5153E4CE8F259766F88CF6ACB8935802A2B97", secondHash);

        var third = new CancellationRecordInput(
            "89890001K", "12345679/G34", new DateOnly(2024, 1, 1),
            secondHash, new DateTimeOffset(2024, 1, 1, 19, 20, 40, TimeSpan.FromHours(1)));
        var thirdHash = _sut.ComputeCancellationHash(third);
        Assert.Equal("177547C0D57AC74748561D054A9CEC14B4C4EA23D1BEFD6F2E69E3A388F90C68", thirdHash);
    }

    [Fact]
    public void ComputeRegistrationHash_IsUppercase64CharHex()
    {
        var input = new RegistrationRecordInput(
            "B12345678", "FA-0001", new DateOnly(2026, 1, 15), "F2", 10.00m, 50.00m, "",
            new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.FromHours(1)));

        var hash = _sut.ComputeRegistrationHash(input);

        Assert.Equal(64, hash.Length);
        Assert.False(hash.Any(char.IsLower), "hash must contain no lowercase characters");
        Assert.Equal(hash, hash.ToUpperInvariant());
    }

    [Fact]
    public void ComputeRegistrationHash_IsDeterministic()
    {
        var input = new RegistrationRecordInput(
            "B12345678", "FA-0001", new DateOnly(2026, 1, 15), "F2", 10.00m, 50.00m, "",
            new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.FromHours(1)));

        Assert.Equal(_sut.ComputeRegistrationHash(input), _sut.ComputeRegistrationHash(input));
    }

    [Fact]
    public void ComputeRegistrationHash_DifferentTipoFactura_ProducesDifferentHash()
    {
        var a = new RegistrationRecordInput("B1", "S1", new DateOnly(2026, 1, 1), "F1", 1m, 1m, "", DateTimeOffset.Now);
        var b = a with { InvoiceType = "F2" };

        Assert.NotEqual(_sut.ComputeRegistrationHash(a), _sut.ComputeRegistrationHash(b));
    }
}
