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
        RecordingEmailSender transport,
        EmailGuardOptions options
    )
    {
        return new ThrottledEmailSender(
            transport,
            options,
            new TestClock(),
            NullLogger<ThrottledEmailSender>.Instance
        );
    }

    [Fact]
    public async Task SendAsync_WithinTheQuota_ForwardsToTheTransport()
    {
        var transport = new RecordingEmailSender();
        var sender = Create(transport, new EmailGuardOptions());

        await sender.SendAsync(Message(), TestContext.Current.CancellationToken);

        transport.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task SendAsync_QuotaExceeded_ThrowsWithoutReachingTheTransport()
    {
        var transport = new RecordingEmailSender();
        var sender = Create(transport, new EmailGuardOptions { RecipientBurst = 1 });

        await sender.SendAsync(Message(), TestContext.Current.CancellationToken);
        var act = () => sender.SendAsync(Message());

        await act.Should().ThrowAsync<EmailRateLimitedException>();
        transport.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task SendAsync_TransportFails_StillSpendsTheQuota()
    {
        var transport = new RecordingEmailSender
        {
            ThrowOnSend = new InvalidOperationException("relay down"),
        };
        var sender = Create(transport, new EmailGuardOptions { RecipientBurst = 1 });

        var first = () => sender.SendAsync(Message());
        await first.Should().ThrowAsync<InvalidOperationException>();

        transport.ThrowOnSend = null;
        var second = () => sender.SendAsync(Message());

        await second.Should().ThrowAsync<EmailRateLimitedException>();
        transport.Sent.Should().BeEmpty();
    }
}
