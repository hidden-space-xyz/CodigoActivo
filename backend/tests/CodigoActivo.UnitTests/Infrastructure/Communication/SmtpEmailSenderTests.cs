using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using MimeKit;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class SmtpEmailSenderTests
{
    private readonly SmtpServerLimits limits = new();

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

    private SmtpEmailSender Create(SmtpOptions options)
    {
        return new SmtpEmailSender(options, limits);
    }

    private async Task SendOneAsync(SmtpOptions options, EmailMessage message, CancellationToken ct)
    {
        await using var session = await Create(options).OpenAsync(ct);
        await session.SendAsync(message, ct);
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
        var message = Message(
            attachments:
            [
                new EmailAttachment("notes.txt", "text/plain", "hello"u8.ToArray()),
                new EmailAttachment("data.bin", "not a content type", [1, 2, 3]),
                new EmailAttachment("forwarded.eml", "message/rfc822", "Subject: x"u8.ToArray()),
                new EmailAttachment(
                    "parts.bin",
                    "multipart/mixed; boundary=x",
                    "--x--"u8.ToArray()
                ),
            ],
            inlineImages: [new EmailInlineImage("logo", "logo.png", "image/png", [137, 80, 78])]
        );

        await SendOneAsync(Options(server, username: "mailer"), message, Timeout());

        server.AuthenticatedUser.Should().Be("mailer");
        server.AuthenticatedPassword.Should().Be("secret");
        var delivered = server.Messages.Should().ContainSingle().Subject;
        delivered.From.Should().Be("no-reply@codigoactivo.test");
        delivered.Recipients.Should().Equal("member@example.test");
        delivered.Message.Subject.Should().Be("Asunto");
        delivered
            .Message.From.Mailboxes.Should()
            .ContainSingle()
            .Which.Name.Should()
            .Be("Codigo Activo");
        delivered.Message.TextBody.Should().Contain("Hola");
        delivered.Message.HtmlBody.Should().Contain("<p>Hola</p>");

        var attachments = delivered.Message.Attachments.OfType<MimePart>().ToList();
        attachments
            .Select(a => (a.FileName, a.ContentType.MimeType))
            .Should()
            .Equal(
                ("notes.txt", "text/plain"),
                ("data.bin", "application/octet-stream"),
                ("forwarded.eml", "application/octet-stream"),
                ("parts.bin", "application/octet-stream")
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
    public async Task SendAsyncSeveralMessagesShareOneConnection()
    {
        await using var server = new FakeSmtpServer();
        await using var session = await Create(Options(server)).OpenAsync(Timeout());

        await session.SendAsync(Message("uno@example.test"), Timeout());
        await session.SendAsync(Message("dos@example.test"), Timeout());
        await session.SendAsync(Message("tres@example.test"), Timeout());

        server.Messages.Should().HaveCount(3);
        server.Connections.Should().Be(1);
    }

    [Fact]
    public async Task SendAsyncConnectionDroppedFailsAsTheServerAndTheNextMessageReconnects()
    {
        await using var server = new FakeSmtpServer { DropConnectionOnDataCommand = 1 };
        await using var session = await Create(Options(server)).OpenAsync(Timeout());

        var first = () => session.SendAsync(Message("uno@example.test"), Timeout());
        (await first.Should().ThrowAsync<EmailDeliveryException>())
            .Which.Failure.Should()
            .Be(EmailDeliveryFailure.ServerUnavailable);
        await session.SendAsync(Message("dos@example.test"), Timeout());

        server.Messages.SelectMany(m => m.Recipients).Should().Equal("dos@example.test");
        server.Connections.Should().Be(2);
    }

    [Fact]
    public async Task OpenAsyncRecordsTheSizeTheServerAnnounces()
    {
        await using var server = new FakeSmtpServer { AdvertisedSize = 10_485_760 };

        await using var session = await Create(Options(server)).OpenAsync(Timeout());

        limits.MaxMessageSize.Should().Be(10_485_760);
    }

    [Fact]
    public async Task SendAsyncOneClickUnsubscribeAddressAddsTheListUnsubscribeHeaders()
    {
        await using var server = new FakeSmtpServer();
        var address =
            $"https://app.test/api/emails/unsubscribe/one-click?token={new string('a', 300)}";

        await SendOneAsync(
            Options(server),
            Message() with
            {
                OneClickUnsubscribeUrl = address,
            },
            Timeout()
        );

        var headers = server.Messages.Should().ContainSingle().Subject.Message.Headers;
        headers
            .Where(header => header.Id == HeaderId.ListUnsubscribe)
            .Should()
            .ContainSingle()
            .Which.Value.Should()
            .Be($"<{address}>");
        headers
            .Where(header => header.Id == HeaderId.ListUnsubscribePost)
            .Should()
            .ContainSingle()
            .Which.Value.Should()
            .Be("List-Unsubscribe=One-Click");
    }

    [Fact]
    public async Task SendAsyncWithoutOneClickUnsubscribeAddressOmitsTheListUnsubscribeHeaders()
    {
        await using var server = new FakeSmtpServer();

        await SendOneAsync(Options(server), Message(), Timeout());

        var headers = server.Messages.Should().ContainSingle().Subject.Message.Headers;
        headers.Contains(HeaderId.ListUnsubscribe).Should().BeFalse();
        headers.Contains(HeaderId.ListUnsubscribePost).Should().BeFalse();
    }

    [Theory]
    [InlineData("", "no-reply@codigoactivo.test")]
    [InlineData("127.0.0.1", "")]
    public async Task OpenAsyncWithoutHostOrSenderIsTheServersFault(string host, string from)
    {
        var sender = Create(new SmtpOptions { Host = host, FromAddress = from });

        var act = () => sender.OpenAsync(TestContext.Current.CancellationToken);

        var thrown = (await act.Should().ThrowAsync<EmailDeliveryException>()).Which;
        thrown.Failure.Should().Be(EmailDeliveryFailure.ServerUnavailable);
        thrown.Message.Should().Contain("SMTP_HOST").And.Contain("SMTP_FROM_ADDRESS");
    }

    [Theory]
    [InlineData(SmtpSecurityMode.StartTls)]
    [InlineData((SmtpSecurityMode)99)]
    [InlineData(SmtpSecurityMode.SslOnConnect)]
    public async Task OpenAsyncEncryptionTheServerCannotSpeakIsTheServersFault(
        SmtpSecurityMode security
    )
    {
        await using var server = new FakeSmtpServer();

        var act = () => Create(Options(server, security)).OpenAsync(Timeout());

        (await act.Should().ThrowAsync<EmailDeliveryException>())
            .Which.Failure.Should()
            .Be(EmailDeliveryFailure.ServerUnavailable);
        server.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsyncRejectedRecipientIsTheRecipientsFaultWithoutTheAddress()
    {
        await using var server = new FakeSmtpServer { RejectedRecipient = "bounce@example.test" };

        var act = () => SendOneAsync(Options(server), Message("bounce@example.test"), Timeout());

        var thrown = (await act.Should().ThrowAsync<EmailDeliveryException>()).Which;
        thrown.Failure.Should().Be(EmailDeliveryFailure.RecipientRejected);
        thrown.ToString().Should().NotContain("bounce@example.test");
    }

    [Theory]
    [InlineData("550 5.1.1 User unknown", EmailDeliveryFailure.RecipientRejected)]
    [InlineData("550 5.2.1 Mailbox disabled", EmailDeliveryFailure.RecipientRejected)]
    [InlineData("554 5.7.1 Relay access denied", EmailDeliveryFailure.Transient)]
    [InlineData("550 5.4.5 Daily sending quota exceeded", EmailDeliveryFailure.Transient)]
    [InlineData("550 Requested action not taken", EmailDeliveryFailure.Transient)]
    public async Task SendAsyncRecipientRefusedForGoodIsTheRecipientsFaultOnlyForABadAddress(
        string reply,
        EmailDeliveryFailure expected
    )
    {
        await using var server = new FakeSmtpServer
        {
            RejectedRecipient = "bounce@example.test",
            RejectedRecipientReply = reply,
        };

        var act = () => SendOneAsync(Options(server), Message("bounce@example.test"), Timeout());

        (await act.Should().ThrowAsync<EmailDeliveryException>())
            .Which.Failure.Should()
            .Be(expected);
    }

    [Theory]
    [InlineData(null, "451 4.7.1 Try again later", EmailDeliveryFailure.Transient)]
    [InlineData(null, "554 5.7.1 Content rejected", EmailDeliveryFailure.Transient)]
    [InlineData(null, "552 5.3.4 Message too big", EmailDeliveryFailure.MessageRejected)]
    [InlineData("451 4.3.0 Busy", null, EmailDeliveryFailure.Transient)]
    public async Task SendAsyncMessageDeferredOrRefusedMayPassUnlessTooLarge(
        string? dataCommandReply,
        string? messageReply,
        EmailDeliveryFailure expected
    )
    {
        await using var server = new FakeSmtpServer
        {
            DataCommandReply = dataCommandReply,
            MessageReply = messageReply,
        };

        var act = () => SendOneAsync(Options(server), Message(), Timeout());

        (await act.Should().ThrowAsync<EmailDeliveryException>())
            .Which.Failure.Should()
            .Be(expected);
    }

    [Fact]
    public async Task SendAsyncConnectionTheServerClosedSilentlyIsOpenedAgain()
    {
        await using var server = new FakeSmtpServer { CloseAfterQueuedMessages = 1 };
        await using var session = await Create(Options(server)).OpenAsync(Timeout());

        await session.SendAsync(Message("one@example.test"), Timeout());
        await session.SendAsync(Message("two@example.test"), Timeout());

        server
            .Messages.SelectMany(m => m.Recipients)
            .Should()
            .Equal("one@example.test", "two@example.test");
        server.Connections.Should().Be(2);
    }

    [Fact]
    public async Task SendAsyncGreylistedRecipientIsDeferredWithoutTheAddress()
    {
        await using var server = new FakeSmtpServer { DeferredRecipient = "later@example.test" };

        var act = () => SendOneAsync(Options(server), Message("later@example.test"), Timeout());

        var thrown = (await act.Should().ThrowAsync<EmailDeliveryException>()).Which;
        thrown.Failure.Should().Be(EmailDeliveryFailure.RecipientDeferred);
        thrown.ToString().Should().NotContain("later@example.test");
    }

    [Theory]
    [InlineData("550 5.7.1 Sender rejected")]
    [InlineData("421 4.7.0 Too many messages, slow down")]
    [InlineData("451 4.7.1 Rate limited")]
    public async Task SendAsyncRefusedSenderOrThrottlingIsTheServersFault(string reply)
    {
        await using var server = new FakeSmtpServer { MailReply = reply };

        var act = () => SendOneAsync(Options(server), Message(), Timeout());

        var thrown = (await act.Should().ThrowAsync<EmailDeliveryException>()).Which;
        thrown.Failure.Should().Be(EmailDeliveryFailure.ServerUnavailable);
        thrown.ToString().Should().NotContain("member@example.test");
    }

    [Fact]
    public async Task SendAsyncAutoSecurityDeliversWithoutAuthentication()
    {
        await using var server = new FakeSmtpServer();

        await SendOneAsync(
            Options(server, SmtpSecurityMode.Auto),
            Message("one@example.test"),
            Timeout()
        );

        server.AuthenticatedUser.Should().BeNull();
        server.Messages.SelectMany(m => m.Recipients).Should().Equal("one@example.test");
    }

    [Fact]
    public async Task SendAsyncCancelledWhileSendingMayPass()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Timeout());
        await using var server = new FakeSmtpServer { OnMessageData = cancellation.Cancel };
        await using var session = await Create(Options(server)).OpenAsync(Timeout());

        var act = () => session.SendAsync(Message("one@example.test"), cancellation.Token);

        var thrown = (await act.Should().ThrowAsync<EmailDeliveryException>()).Which;
        thrown.Failure.Should().Be(EmailDeliveryFailure.Transient);
        thrown.InnerException.Should().BeAssignableTo<OperationCanceledException>();
    }
}
