using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
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
/// <see cref="EmailOutboxDeliveryWorker"/> over the real store and lock with a transport of its own,
/// and stops and disposes it before the test ends.
/// </summary>
public sealed class EmailOutboxDeliveryWorkerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private IServiceScopeFactory Scopes =>
        Factory.Services.GetRequiredService<IServiceScopeFactory>();

    [Fact]
    public async Task ExecuteAsyncStoredEmailIsDeliveredByTheHostedWorkerAndLeavesTheOutboxEmpty()
    {
        var options = new EmailQueueOptions { PollInterval = TimeSpan.FromMilliseconds(50) };
        var guard = new EmailGuardOptions();
        var smtp = new SmtpOptions();
        var signal = new EmailOutboxSignal();
        var protector = Factory.Services.GetRequiredService<EmailOutboxProtector>();
        var store = new EmailOutboxStore(
            Scopes,
            protector,
            signal,
            new SmtpServerLimits(),
            options,
            Factory.Clock
        );
        var transport = new AwaitedEmailTransport();
        await using var deliveryLock = new EmailDeliveryLock(Scopes);
        using var processor = new EmailOutboxDeliveryWorker(
            new EmailOutboxDeliverer(
                store,
                transport,
                protector,
                new EmailDeliveryPacer(smtp, guard, Factory.Clock),
                options,
                smtp,
                guard,
                Factory.Clock,
                NullLogger<EmailOutboxDeliverer>.Instance
            ),
            deliveryLock,
            signal,
            options,
            guard,
            Factory.Clock,
            NullLogger<EmailOutboxDeliveryWorker>.Instance
        );
        var sender = new ThrottledEmailSender(
            store,
            guard,
            smtp,
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

    [Fact]
    public async Task HoldAsyncOnlyOneInstanceDeliversAtATime()
    {
        await using var first = new EmailDeliveryLock(Scopes);
        await using var second = new EmailDeliveryLock(Scopes);

        (await first.HoldAsync(Ct)).Should().BeTrue();
        (await first.HoldAsync(Ct)).Should().BeTrue("the holder keeps the lock on every poll");
        (await second.HoldAsync(Ct)).Should().BeFalse("another instance already delivers");

        await first.DisposeAsync();

        (await second.HoldAsync(Ct)).Should().BeTrue("a released lock is taken over at once");
    }

    private sealed class AwaitedEmailTransport : IEmailTransport, IEmailTransportSession
    {
        public TaskCompletionSource<EmailMessage> Delivered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IEmailTransportSession> OpenAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IEmailTransportSession>(this);
        }

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Delivered.TrySetResult(message);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
