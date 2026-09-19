using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

/// <summary>
/// Exercises the hosted delivery worker itself against PostgreSQL. The shared factory drains the
/// outbox inline so tests never race a background service, so this one wires the real
/// <see cref="EmailOutboxProcessor"/> over the real store with a transport of its own, and stops and
/// disposes it before the test ends.
/// </summary>
public sealed class EmailOutboxProcessorTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task ExecuteAsyncStoredEmailIsDeliveredByTheHostedWorkerAndLeavesTheOutboxEmpty()
    {
        var options = new EmailQueueOptions { PollInterval = TimeSpan.FromMilliseconds(50) };
        var signal = new EmailOutboxSignal();
        var protector = Factory.Services.GetRequiredService<EmailOutboxProtector>();
        var store = new EmailOutboxStore(
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            protector,
            signal,
            options,
            Factory.Clock
        );
        var transport = new AwaitedEmailTransport();
        using var processor = new EmailOutboxProcessor(
            new EmailOutboxDeliverer(
                store,
                transport,
                protector,
                options,
                Factory.Clock,
                NullLogger<EmailOutboxDeliverer>.Instance
            ),
            signal,
            options,
            NullLogger<EmailOutboxProcessor>.Instance
        );
        var sender = new ThrottledEmailSender(
            store,
            new EmailGuardOptions(),
            options,
            Factory.Clock,
            NullLogger<ThrottledEmailSender>.Instance
        );

        EmailMessage delivered;
        await processor.StartAsync(Ct);
        try
        {
            await sender.SendAsync(
                new EmailMessage(
                    EmailKind.TwoFactorCode,
                    "ana@example.test",
                    "Ana",
                    "Tu código de acceso",
                    "<p>482913</p>",
                    "482913"
                ),
                Ct
            );

            delivered = await transport.Delivered.Task.WaitAsync(Patience, Ct);
        }
        finally
        {
            await processor.StopAsync(Ct);
        }

        delivered
            .Should()
            .BeEquivalentTo(new { ToAddress = "ana@example.test", TextBody = "482913" });
        var pending = await Factory.QueryAsync(db =>
            db.EmailOutboxMessages.AsNoTracking().CountAsync(Ct)
        );
        var contents = await Factory.QueryAsync(db =>
            db.EmailOutboxContents.AsNoTracking().CountAsync(Ct)
        );
        pending.Should().Be(0, "the worker removes what it delivered");
        contents.Should().Be(0);
    }

    private sealed class AwaitedEmailTransport : IEmailTransport
    {
        public TaskCompletionSource<EmailMessage> Delivered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Delivered.TrySetResult(message);
            return Task.CompletedTask;
        }
    }
}
