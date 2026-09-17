using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class SmtpEmailSenderTests
{
    private static SmtpOptions Options(
        FakeSmtpServer server,
        SmtpSecurityMode security = SmtpSecurityMode.None,
        string username = ""
    )
    {
        return new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = server.Port,
            Security = security,
            Username = username,
            Password = username.Length > 0 ? "secret" : string.Empty,
            FromAddress = "no-reply@codigoactivo.test",
            FromName = "Codigo Activo",
        };
    }

    private static SmtpEmailSender Create(SmtpOptions options)
    {
        return new SmtpEmailSender(options, NullLogger<SmtpEmailSender>.Instance);
    }

    private static EmailMessage Message(
        string to = "member@example.test",
        IReadOnlyList<EmailAttachment>? attachments = null,
        IReadOnlyList<EmailInlineImage>? inlineImages = null
    )
    {
        return new EmailMessage(
            EmailKind.Manual,
            to,
            "Ana",
            "Asunto",
            "<p>Hola</p>",
            "Hola",
            attachments,
            inlineImages
        );
    }

    private static CancellationToken Timeout()
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        source.CancelAfter(TimeSpan.FromSeconds(30));
        return source.Token;
    }

    [Fact]
    public async Task SendAsyncAuthenticatedServerDeliversBodiesAttachmentsAndInlineImages()
    {
        await using var server = new FakeSmtpServer();
        var sender = Create(Options(server, username: "mailer"));
        var message = Message(
            attachments:
            [
                new EmailAttachment("notes.txt", "text/plain", "hello"u8.ToArray()),
                new EmailAttachment("data.bin", "not a content type", [1, 2, 3]),
                new EmailAttachment("forwarded.eml", "message/rfc822", "Subject: x"u8.ToArray()),
            ],
            inlineImages: [new EmailInlineImage("logo", "logo.png", "image/png", [137, 80, 78])]
        );

        await sender.SendAsync(message, Timeout());

        server.AuthenticatedUser.Should().Be("mailer");
        server.AuthenticatedPassword.Should().Be("secret");
        var delivered = server.Messages.Should().ContainSingle().Subject;
        delivered.From.Should().Be("no-reply@codigoactivo.test");
        delivered.Recipients.Should().Equal("member@example.test");
        delivered.Message.Subject.Should().Be("Asunto");
        delivered.Message.From.Mailboxes.Should().ContainSingle().Which.Name.Should().Be("Codigo Activo");
        delivered.Message.TextBody.Should().Contain("Hola");
        delivered.Message.HtmlBody.Should().Contain("<p>Hola</p>");

        var attachments = delivered.Message.Attachments.OfType<MimePart>().ToList();
        attachments
            .Select(a => (a.FileName, a.ContentType.MimeType))
            .Should()
            .Equal(
                ("notes.txt", "text/plain"),
                ("data.bin", "application/octet-stream"),
                ("forwarded.eml", "application/octet-stream")
            );

        var inline = delivered
            .Message.BodyParts.OfType<MimePart>()
            .Single(part => part.ContentId == "logo");
        inline.ContentType.MimeType.Should().Be("image/png");
        inline.ContentDisposition.Should().NotBeNull();
        inline.ContentDisposition!.Disposition.Should().Be(ContentDisposition.Inline);
        inline.FileName.Should().Be("logo.png");
    }

    [Fact]
    public async Task SendAsyncWithoutHostThrowsInvalidOperation()
    {
        var sender = Create(new SmtpOptions { FromAddress = "no-reply@codigoactivo.test" });

        var act = () => sender.SendAsync(Message(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*SMTP_HOST*");
    }

    [Fact]
    public async Task SendAsyncWithoutFromAddressThrowsInvalidOperation()
    {
        var sender = Create(new SmtpOptions { Host = "127.0.0.1" });

        var act = () => sender.SendAsync(Message(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*SMTP_FROM_ADDRESS*");
    }

    [Theory]
    [InlineData(SmtpSecurityMode.StartTls)]
    [InlineData((SmtpSecurityMode)99)]
    public async Task SendAsyncStartTlsWithoutServerSupportThrowsWithoutDelivering(
        SmtpSecurityMode security
    )
    {
        await using var server = new FakeSmtpServer();
        var sender = Create(Options(server, security));

        var act = () => sender.SendAsync(Message(), Timeout());

        await act.Should().ThrowAsync<NotSupportedException>();
        server.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsyncSslOnConnectAgainstPlainServerFailsWithoutDelivering()
    {
        await using var server = new FakeSmtpServer();
        var sender = Create(Options(server, SmtpSecurityMode.SslOnConnect));

        var act = () => sender.SendAsync(Message(), Timeout());

        await act.Should().ThrowAsync<Exception>();
        server.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendManyAsyncNoMessagesReturnsEmptyResultWithoutConfiguration()
    {
        var sender = Create(new SmtpOptions());

        var result = await sender.SendManyAsync([], TestContext.Current.CancellationToken);

        result.Should().Be(new EmailBatchResult(0, 0));
    }

    [Fact]
    public async Task SendManyAsyncWithoutFromAddressThrowsInvalidOperation()
    {
        var sender = Create(new SmtpOptions { Host = "127.0.0.1" });

        var act = () => sender.SendManyAsync([Message()], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendManyAsyncAutoSecurityDeliversEveryMessageOverOneConnection()
    {
        await using var server = new FakeSmtpServer();
        var sender = Create(Options(server, SmtpSecurityMode.Auto));

        var result = await sender.SendManyAsync(
            [Message("one@example.test"), Message("two@example.test")],
            Timeout()
        );

        result.Should().Be(new EmailBatchResult(2, 0));
        server.AuthenticatedUser.Should().BeNull();
        server
            .Messages.SelectMany(m => m.Recipients)
            .Should()
            .Equal("one@example.test", "two@example.test");
    }

    [Fact]
    public async Task SendManyAsyncRejectedRecipientCountsFailureAndContinues()
    {
        await using var server = new FakeSmtpServer { RejectedRecipient = "bounce@example.test" };
        var sender = Create(Options(server));

        var result = await sender.SendManyAsync(
            [Message("one@example.test"), Message("bounce@example.test"), Message("two@example.test")],
            Timeout()
        );

        result.Should().Be(new EmailBatchResult(2, 1));
        server
            .Messages.SelectMany(m => m.Recipients)
            .Should()
            .Equal("one@example.test", "two@example.test");
    }

    [Fact]
    public async Task SendManyAsyncConnectionDroppedCountsUnattemptedMessagesAsFailed()
    {
        await using var server = new FakeSmtpServer { DropConnectionOnDataCommand = 2 };
        var sender = Create(Options(server));

        var result = await sender.SendManyAsync(
            [
                Message("one@example.test"),
                Message("two@example.test"),
                Message("three@example.test"),
                Message("four@example.test"),
            ],
            Timeout()
        );

        result.Should().Be(new EmailBatchResult(1, 3));
        server.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task SendManyAsyncCancelledMidBatchStopsAndThrows()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Timeout());
        await using var server = new FakeSmtpServer { OnMessageData = cancellation.Cancel };
        var sender = Create(Options(server));

        var act = () =>
            sender.SendManyAsync(
                [Message("one@example.test"), Message("two@example.test")],
                cancellation.Token
            );

        await act.Should().ThrowAsync<OperationCanceledException>();
        server.Messages.Select(m => m.Recipients).Should().NotContain(r => r.Contains("two@example.test"));
    }
}
