using AwesomeAssertions;
using CodigoActivo.Infrastructure.Security;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Security;

public sealed class TotpServiceTests
{
    /// <summary>
    /// The RFC 6238 reference secret "12345678901234567890" in Base32.
    /// </summary>
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    private readonly TotpService sut = new();

    private static DateTimeOffset Instant(long unixSeconds)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }

    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1_111_111_109, "081804")]
    [InlineData(1_111_111_111, "050471")]
    [InlineData(1_234_567_890, "005924")]
    [InlineData(2_000_000_000, "279037")]
    public void ComputeCodeRfc6238VectorsProduceTheLastSixDigitsOfTheReferenceValues(
        long unixSeconds,
        string expected
    )
    {
        TotpService.ComputeCode(RfcSecret, TotpService.StepOf(Instant(unixSeconds))).Should().Be(expected);
    }

    [Fact]
    public void MatchStepCurrentCodeReturnsTheCurrentStep()
    {
        var now = Instant(1_111_111_111);

        sut.MatchStep(RfcSecret, "050471", now).Should().Be(TotpService.StepOf(now));
    }

    [Fact]
    public void MatchStepPreviousAndNextStepsAreToleratedAsClockDrift()
    {
        var now = Instant(1_111_111_111);
        var step = TotpService.StepOf(now);

        sut.MatchStep(RfcSecret, TotpService.ComputeCode(RfcSecret, step - 1), now).Should().Be(step - 1);
        sut.MatchStep(RfcSecret, TotpService.ComputeCode(RfcSecret, step + 1), now).Should().Be(step + 1);
    }

    [Fact]
    public void MatchStepCodeTwoStepsAwayIsRejected()
    {
        var now = Instant(1_111_111_111);
        var step = TotpService.StepOf(now);

        sut.MatchStep(RfcSecret, TotpService.ComputeCode(RfcSecret, step - 2), now).Should().BeNull();
        sut.MatchStep(RfcSecret, TotpService.ComputeCode(RfcSecret, step + 2), now).Should().BeNull();
    }

    [Fact]
    public void MatchStepAcceptsSpacesAndLowercaseSecrets()
    {
        var now = Instant(1_111_111_111);

        sut.MatchStep("gezd gnbv gy3t qojq gezd gnbv gy3t qojq", "050 471", now)
            .Should()
            .Be(TotpService.StepOf(now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("05047")]
    [InlineData("0504711")]
    [InlineData("05047a")]
    [InlineData("000000")]
    public void MatchStepMalformedOrWrongCodesAreRejected(string code)
    {
        sut.MatchStep(RfcSecret, code, Instant(1_111_111_111)).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base32!")]
    [InlineData("1")]
    public void MatchStepInvalidSecretIsRejected(string secret)
    {
        sut.MatchStep(secret, "050471", Instant(1_111_111_111)).Should().BeNull();
    }

    [Fact]
    public void ComputeCodeInvalidSecretThrows()
    {
        var act = () => TotpService.ComputeCode("not base32!", 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateSecretProducesRandomThirtyTwoCharacterBase32Secrets()
    {
        var first = sut.GenerateSecret();
        var second = sut.GenerateSecret();

        first.Should().HaveLength(32).And.MatchRegex("^[A-Z2-7]+$");
        second.Should().NotBe(first);
        sut.MatchStep(first, TotpService.ComputeCode(first, 1_000), Instant(30_000)).Should().Be(1_000);
    }
}
