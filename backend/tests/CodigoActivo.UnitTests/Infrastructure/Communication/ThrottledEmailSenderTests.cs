using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class ThrottledEmailSenderTests
{
    private static EmailMessage Message(
        EmailKind kind = EmailKind.ActivityNotification,
        string address = "member@example.test"
    )
    {
        return new EmailMessage(kind, address, "Ana", "Asunto", "<p>Hola</p>", "Hola");
    }

    private static ThrottledEmailSender Create(
        RecordingEmailOutbox outbox,
        EmailGuardOptions options,
        ILogger<ThrottledEmailSender>? logger = null
    )
    {
        return new ThrottledEmailSender(
            outbox,
            options,
            new EmailQueueOptions(),
            new TestClock(),
            logger ?? NullLogger<ThrottledEmailSender>.Instance
        );
    }

    [Fact]
    public async Task SendAsyncWithinTheQuotaStoresTheMessage()
    {
        var outbox = new RecordingEmailOutbox();
        var sender = Create(outbox, new EmailGuardOptions());

        await sender.SendAsync(Message(), TestContext.Current.CancellationToken);

        outbox.Messages.Should().ContainSingle().Which.ToAddress.Should().Be("member@example.test");
    }

    [Fact]
    public async Task SendAsyncQuotaExceededThrowsWithoutStoring()
    {
        var outbox = new RecordingEmailOutbox();
        var sender = Create(outbox, new EmailGuardOptions { RecipientBurst = 1 });

        await sender.SendAsync(Message(), TestContext.Current.CancellationToken);
        var act = () => sender.SendAsync(Message(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<EmailRateLimitedException>();
        outbox.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task SendAsyncOutboxFullThrowsAndStillSpendsTheQuota()
    {
        var outbox = new RecordingEmailOutbox { RejectAll = true };
        var sender = Create(outbox, new EmailGuardOptions { RecipientBurst = 1 });

        var first = () => sender.SendAsync(Message(), TestContext.Current.CancellationToken);
        await first.Should().ThrowAsync<EmailRateLimitedException>();

        outbox.RejectAll = false;
        var second = () => sender.SendAsync(Message(), TestContext.Current.CancellationToken);

        await second.Should().ThrowAsync<EmailRateLimitedException>();
        outbox.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsyncTrackedRecipientsSaturatedWarnsAndStillStoresTheMessage()
    {
        var logger = new RecordingLogger<ThrottledEmailSender>();
        var outbox = new RecordingEmailOutbox();
        var sender = Create(outbox, new EmailGuardOptions { MaxTrackedRecipients = 1 }, logger);

        await sender.SendAsync(Message(), TestContext.Current.CancellationToken);
        await sender.SendAsync(
            Message(address: "otro@example.test"),
            TestContext.Current.CancellationToken
        );

        outbox.Messages.Should().HaveCount(2);
        logger
            .LevelEntries.Should()
            .ContainSingle(entry => entry.Level == LogLevel.Warning)
            .Which.Message.Should()
            .Contain("1");
        logger.Entries.Should().NotContain(entry => entry.Contains("otro@example.test"));
    }

    [Fact]
    public async Task SendAsyncOutboxFullLogsTheCapacityWithoutTheAddress()
    {
        var logger = new RecordingLogger<ThrottledEmailSender>();
        var outbox = new RecordingEmailOutbox { RejectAll = true };
        var sender = Create(outbox, new EmailGuardOptions(), logger);

        var act = () => sender.SendAsync(Message(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<EmailRateLimitedException>())
            .Which.Scope.Should()
            .Be(EmailLimitScope.Global);
        logger.Entries.Should().ContainSingle().Which.Should().Contain("1000");
        logger.Entries.Should().NotContain(entry => entry.Contains("member@example.test"));
    }
}
