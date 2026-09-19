using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class EmailOutboxProtectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly EmailOutboxProtector sut = new(new EphemeralDataProtectionProvider());

    private static EmailBatch Batch()
    {
        return new EmailBatch(
            EmailKind.Manual,
            "Asamblea general",
            "<p>Os esperamos</p>",
            "Os esperamos",
            [new EmailRecipient("ana@example.test", "Ana")],
            [new EmailAttachment("acta.pdf", "application/pdf", "contenido"u8.ToArray())],
            [new EmailInlineImage("logo", "logo.png", "image/png", [137, 80, 78])]
        );
    }

    [Fact]
    public void ToContentStoresNoReadableSubjectBodyOrAttachment()
    {
        var content = sut.ToContent(Batch(), Now);

        Readable(content.Subject).Should().NotContain("Asamblea");
        Readable(content.HtmlBody).Should().NotContain("Os esperamos");
        Readable(content.TextBody).Should().NotContain("Os esperamos");
        content
            .Parts.Should()
            .AllSatisfy(part => Readable(part.Payload).Should().NotContain("contenido"));
        content.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void ToContentKeepsTheEnvelopeOfEveryPartReadable()
    {
        var content = sut.ToContent(Batch(), Now);

        content
            .Parts.Select(part => (part.Disposition, part.FileName, part.InlineContentId))
            .Should()
            .Equal(
                (EmailPartDisposition.Inline, "logo.png", "logo"),
                (EmailPartDisposition.Attachment, "acta.pdf", null)
            );
    }

    [Fact]
    public void ToMessageRebuildsEveryFieldOfTheStoredEmail()
    {
        var batch = Batch();
        var content = sut.ToContent(batch, Now);
        var stored = new EmailOutboxMessage
        {
            Content = content,
            ContentId = content.Id,
            Kind = batch.Kind,
            ToAddress = "ana@example.test",
            ToName = "Ana",
        };

        var message = EmailOutboxProtector.ToMessage(stored, sut.Unprotect(content));

        message.Kind.Should().Be(EmailKind.Manual);
        message.ToAddress.Should().Be("ana@example.test");
        message.ToName.Should().Be("Ana");
        message.Subject.Should().Be("Asamblea general");
        message.HtmlBody.Should().Be("<p>Os esperamos</p>");
        message.TextBody.Should().Be("Os esperamos");
        message
            .Attachments.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(batch.Attachments![0]);
        message
            .InlineImages.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(batch.InlineImages![0]);
    }

    [Fact]
    public void UnprotectContentOfAnotherKeyRingThrowsCryptographicException()
    {
        var content = new EmailOutboxProtector(new EphemeralDataProtectionProvider()).ToContent(
            Batch(),
            Now
        );

        var act = () => sut.Unprotect(content);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void ToMessageEveryRecipientSharesTheBinaryContentOfThePayload()
    {
        var content = sut.ToContent(Batch(), Now);
        var payload = sut.Unprotect(content);
        var first = new EmailOutboxMessage { Content = content, ToAddress = "ana@example.test" };
        var second = new EmailOutboxMessage { Content = content, ToAddress = "berto@example.test" };

        var one = EmailOutboxProtector.ToMessage(first, payload);
        var other = EmailOutboxProtector.ToMessage(second, payload);

        one.Attachments.Should().BeSameAs(other.Attachments);
        one.Attachments![0].Content.Should().BeSameAs(other.Attachments![0].Content);
        one.InlineImages![0].Content.Should().BeSameAs(other.InlineImages![0].Content);
        one.ToAddress.Should().Be("ana@example.test");
        other.ToAddress.Should().Be("berto@example.test");
    }

    [Fact]
    public void ToContentWithoutBinaryPartsStoresOnlyTheText()
    {
        var content = sut.ToContent(
            new EmailBatch(
                EmailKind.TwoFactorCode,
                "Código",
                "<p>1</p>",
                "1",
                [new EmailRecipient("ana@example.test", "Ana")]
            ),
            Now
        );

        content.Parts.Should().BeEmpty();
    }

    private static string Readable(byte[] payload)
    {
        return Encoding.UTF8.GetString(payload);
    }
}
