using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class ThrottledEmailSenderTests
{
    private static EmailMessage Message(EmailKind kind = EmailKind.ActivityNotification)
    {
        return new EmailMessage(kind, "member@example.test", "Ana", "Asunto", "<p>Hola</p>", "Hola");
    }

    private static ThrottledEmailSender Create(
        RecordingEmailDispatcher dispatcher,
        EmailGuardOptions options
    )
    {
        return new ThrottledEmailSender(
            dispatcher,
            options,
            new EmailQueueOptions(),
            new TestClock(),
            NullLogger<ThrottledEmailSender>.Instance
        );
    }

    [Fact]
    public async Task SendAsync_WithinTheQuota_EnqueuesTheMessage()
    {
        var queue = new RecordingEmailDispatcher();
        var sender = Create(queue, new EmailGuardOptions());

        await sender.SendAsync(Message(), TestContext.Current.CancellationToken);

        queue.Enqueued.Should().ContainSingle();
    }

    [Fact]
    public async Task SendAsync_QuotaExceeded_ThrowsWithoutEnqueuing()
    {
        var queue = new RecordingEmailDispatcher();
        var sender = Create(queue, new EmailGuardOptions { RecipientBurst = 1 });

        await sender.SendAsync(Message(), TestContext.Current.CancellationToken);
        var act = () => sender.SendAsync(Message(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<EmailRateLimitedException>();
        queue.Enqueued.Should().ContainSingle();
    }

    [Fact]
    public async Task SendAsync_QueueFull_ThrowsAndStillSpendsTheQuota()
    {
        var queue = new RecordingEmailDispatcher { RejectAll = true };
        var sender = Create(queue, new EmailGuardOptions { RecipientBurst = 1 });

        var first = () => sender.SendAsync(Message(), TestContext.Current.CancellationToken);
        await first.Should().ThrowAsync<EmailRateLimitedException>();

        queue.RejectAll = false;
        var second = () => sender.SendAsync(Message(), TestContext.Current.CancellationToken);

        await second.Should().ThrowAsync<EmailRateLimitedException>();
        queue.Enqueued.Should().BeEmpty();
    }
}
