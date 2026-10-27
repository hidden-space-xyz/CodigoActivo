using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class ThrottledEmailSenderTests
{
    private static EmailMessage Message(
        EmailKind kind = EmailKind.AccessRecovery,
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
            new SmtpOptions(),
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
    public async Task SendAsyncCodeOverTheQuotaThrowsWithoutStoring()
    {
        var outbox = new RecordingEmailOutbox();
        var sender = Create(outbox, new EmailGuardOptions { RecipientBurst = 1 });

        await sender.SendAsync(Message(), TestContext.Current.CancellationToken);
        var act = () => sender.SendAsync(Message(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<EmailRateLimitedException>();
        outbox.Messages.Should().ContainSingle();
    }

    [Theory]
    [InlineData(EmailKind.SecurityAlert)]
    [InlineData(EmailKind.ActivityNotification)]
    [InlineData(EmailKind.ComingOfAgeNotice)]
    [InlineData(EmailKind.ConsentWithdrawalNotice)]
    [InlineData(EmailKind.BlockedDataConsultationNotice)]
    public async Task SendAsyncNoticeOverTheQuotaIsStoredAnyway(EmailKind kind)
    {
        var outbox = new RecordingEmailOutbox();
        var sender = Create(
            outbox,
            new EmailGuardOptions
            {
                RecipientBurst = 1,
                AnonymousBurst = 1,
                AnonymousPerHour = 1,
            }
        );

        for (var sent = 0; sent < 3; sent++)
        {
            await sender.SendAsync(Message(kind), TestContext.Current.CancellationToken);
        }

        outbox.Messages.Should().HaveCount(3, "notices are paced when delivered, never refused");
    }

    [Fact]
    public async Task SendAsyncOutboxRefusesTheMessageThrowsWithoutTheAddress()
    {
        var outbox = new RecordingEmailOutbox { Answer = EmailEnqueueResult.TooLarge };
        var sender = Create(outbox, new EmailGuardOptions());

        var act = () =>
            sender.SendAsync(
                Message(EmailKind.SecurityAlert),
                TestContext.Current.CancellationToken
            );

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should()
            .NotContain("member@example.test");
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
    public async Task SendAsyncAnonymousBudgetSpentLogsOnceAndKeepsLoginCodesFlowing()
    {
        var logger = new RecordingLogger<ThrottledEmailSender>();
        var outbox = new RecordingEmailOutbox();
        var sender = Create(
            outbox,
            new EmailGuardOptions { AnonymousBurst = 1, AnonymousPerHour = 1 },
            logger
        );

        await sender.SendAsync(
            Message(address: "a@example.test"),
            TestContext.Current.CancellationToken
        );
        var denied = () =>
            sender.SendAsync(
                Message(address: "b@example.test"),
                TestContext.Current.CancellationToken
            );
        (await denied.Should().ThrowAsync<EmailRateLimitedException>())
            .Which.Scope.Should()
            .Be(EmailLimitScope.Global);
        await sender.SendAsync(
            Message(EmailKind.TwoFactorCode, "c@example.test"),
            TestContext.Current.CancellationToken
        );

        outbox
            .Messages.Select(message => message.ToAddress)
            .Should()
            .Equal("a@example.test", "c@example.test");
        logger.LevelEntries.Should().ContainSingle(entry => entry.Level == LogLevel.Error);
    }
}
