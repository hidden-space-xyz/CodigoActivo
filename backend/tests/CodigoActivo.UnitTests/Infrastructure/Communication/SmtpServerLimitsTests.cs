using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class SmtpServerLimitsTests
{
    private static EmailBatch Batch(int attachmentBytes)
    {
        return new EmailBatch(
            EmailKind.Manual,
            "Asamblea",
            "<p>Hola</p>",
            "Hola",
            [new EmailRecipient("ana@example.test", "Ana")],
            [new EmailAttachment("acta.pdf", "application/pdf", new byte[attachmentBytes])]
        );
    }

    [Fact]
    public void AcceptsBeforeAnyConnectionAcceptsEverySize()
    {
        new SmtpServerLimits().Accepts(Batch(50_000_000)).Should().BeTrue();
    }

    [Fact]
    public void AcceptsServerWithoutALimitAcceptsEverySize()
    {
        var limits = new SmtpServerLimits();
        limits.Record(0);

        limits.MaxMessageSize.Should().BeNull();
        limits.Accepts(Batch(50_000_000)).Should().BeTrue();
    }

    [Fact]
    public void AcceptsCountsTheAttachmentsAsEncodedForTheWire()
    {
        var limits = new SmtpServerLimits();
        limits.Record(10_000_000);

        limits.Accepts(Batch(7_000_000)).Should().BeTrue();
        limits
            .Accepts(Batch(7_500_000))
            .Should()
            .BeFalse("base64 makes 7.5 MB of attachments about 10.3 MB on the wire");
    }
}
