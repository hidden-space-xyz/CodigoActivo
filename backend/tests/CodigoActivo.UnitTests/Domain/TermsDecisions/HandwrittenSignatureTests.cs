using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.TermsDecisions;

public sealed class HandwrittenSignatureTests
{
    [Fact]
    public void CreatePngWithinLimitsKeepsACopy()
    {
        var png = SignatureImages.Png(
            HandwrittenSignature.MaxWidth,
            HandwrittenSignature.MaxHeight,
            HandwrittenSignature.MaxBytes
        );

        var signature = HandwrittenSignature.Create(png).Value;
        var stored = (byte[])png.Clone();
        png[^1] = 0xFF;

        signature.Png.ToArray().Should().Equal(stored);
    }

    [Theory]
    [InlineData(HandwrittenSignature.MaxWidth + 1, 200)]
    [InlineData(600, HandwrittenSignature.MaxHeight + 1)]
    [InlineData(0, 200)]
    [InlineData(600, 0)]
    public void CreateImageOutsideTheSizeLimitsIsRefused(uint width, uint height)
    {
        HandwrittenSignature
            .Create(SignatureImages.Png(width, height))
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.EventTermsSignatureInvalid);
    }

    [Fact]
    public void CreateImageLargerThanTheByteLimitIsRefused()
    {
        HandwrittenSignature
            .Create(SignatureImages.Png(length: HandwrittenSignature.MaxBytes + 1))
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.EventTermsSignatureInvalid);
    }

    [Fact]
    public void CreateContentThatIsNotPngIsRefused()
    {
        var jpeg = SignatureImages.Png();
        jpeg[0] = 0xFF;

        HandwrittenSignature
            .Create(jpeg)
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.EventTermsSignatureInvalid);
    }

    [Fact]
    public void FromStoredThenToArrayReturnsACopyOfTheStoredBytes()
    {
        var png = SignatureImages.Png();
        var signature = HandwrittenSignature.FromStored(png);

        var copy = signature.ToArray();
        copy[0] = 0;

        copy.Should().NotBeSameAs(png);
        signature.ToArray().Should().Equal(png);
    }
}
