using System.Text;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common;
using CodigoActivo.Infrastructure.Communication.Templates;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class ManualEmailComposerTests
{
    private readonly ManualEmailComposer sut = new(
        new ApplicationOptions { BaseUrl = "https://app.test" }
    );

    [Fact]
    public void ComposeValidRequestRendersTheTextForEveryRecipientWithTheAttachments()
    {
        EmailRecipient[] recipients =
        [
            new("ana@test.local", "Ana"),
            new("berto@test.local", "Berto"),
        ];
        EmailAttachment[] attachments =
        [
            new("acta.pdf", "application/pdf", Encoding.UTF8.GetBytes("acta")),
        ];

        var batch = sut.Compose("Asunto", "Nos vemos el sábado", recipients, attachments);

        batch.Kind.Should().Be(EmailKind.Manual);
        batch.Subject.Should().Be("Asunto");
        batch.TextBody.Should().Contain("Nos vemos el sábado");
        batch.Recipients.Should().Equal(recipients);
        batch.Attachments.Should().Equal(attachments);
    }
}
