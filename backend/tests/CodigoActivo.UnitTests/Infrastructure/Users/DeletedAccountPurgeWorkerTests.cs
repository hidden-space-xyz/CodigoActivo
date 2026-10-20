using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Domain.Users.Erasure;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Users;

public sealed class DeletedAccountPurgeWorkerTests : IDisposable
{
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(10);

    private static readonly DeletedAccountPurgeOptions Immediate = new()
    {
        StartupDelay = TimeSpan.Zero,
        Interval = TimeSpan.FromHours(1),
    };

    private readonly IDeletedAccountRepository deletedAccounts =
        Substitute.For<IDeletedAccountRepository>();
    private readonly IDeletedParticipationRepository deletedParticipations =
        Substitute.For<IDeletedParticipationRepository>();
    private readonly TestClock clock = new();
    private readonly RecordingLogger<DeletedAccountPurgeWorker> logger = new();
    private readonly ServiceProvider provider;

    public DeletedAccountPurgeWorkerTests()
    {
        provider = new ServiceCollection()
            .AddScoped<IDeletedAccountRepository>(_ => deletedAccounts)
            .AddScoped<IDeletedParticipationRepository>(_ => deletedParticipations)
            .AddSingleton<IClock>(clock)
            .AddScoped<
                ICommandHandler<PurgeDeletedAccountsCommand, Result<int>>,
                PurgeDeletedAccountsCommandHandler
            >()
            .AddScoped<
                ICommandHandler<PurgeDeletedParticipationsCommand, Result<int>>,
                PurgeDeletedParticipationsCommandHandler
            >()
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        provider.Dispose();
    }

    private DeletedAccountPurgeWorker Build(DeletedAccountPurgeOptions? options = null)
    {
        return new DeletedAccountPurgeWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options ?? new DeletedAccountPurgeOptions(),
            logger
        );
    }

    private void PurgesAndSignals(TaskCompletionSource signal, int purged)
    {
        deletedAccounts
            .PurgeDeletedUpToAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                signal.TrySetResult();
                return purged;
            });
    }

    [Fact]
    public async Task PurgeAsyncPurgesCopiesDeletedTwoYearsBeforeTheCurrentClockTime()
    {
        deletedAccounts
            .PurgeDeletedUpToAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(4);
        deletedParticipations
            .PurgeDeletedUpToAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(3);

        var purged = await Build().PurgeAsync(TestContext.Current.CancellationToken);

        purged.Should().Be((4, 3));
        await deletedAccounts
            .Received(1)
            .PurgeDeletedUpToAsync(clock.UtcNow.AddYears(-2), Arg.Any<CancellationToken>());
        await deletedParticipations
            .Received(1)
            .PurgeDeletedUpToAsync(clock.UtcNow.AddYears(-2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncFirstRunPurgesAfterTheStartupDelayWithoutLogging()
    {
        var signal = new TaskCompletionSource();
        PurgesAndSignals(signal, purged: 2);
        var purger = Build(Immediate);

        await purger.StartAsync(TestContext.Current.CancellationToken);
        await signal.Task.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        await purger.StopAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsyncFailedRunIsLoggedAndNeverFaultsTheHost()
    {
        var signal = new TaskCompletionSource();
        deletedAccounts
            .PurgeDeletedUpToAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
            {
                signal.TrySetResult();
                throw new InvalidOperationException("db down");
            });
        var purger = Build(Immediate);

        await purger.StartAsync(TestContext.Current.CancellationToken);
        await signal.Task.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        await purger.StopAsync(TestContext.Current.CancellationToken);

        purger.ExecuteTask!.IsFaulted.Should().BeFalse();
        logger
            .LevelEntries.Should()
            .Contain(entry => entry.Level == LogLevel.Error && entry.Message.Contains("db down"));
    }

    [Fact]
    public async Task ExecuteAsyncShutdownBeforeTheFirstRunTouchesNothing()
    {
        var purger = Build(
            new DeletedAccountPurgeOptions
            {
                StartupDelay = TimeSpan.FromHours(1),
                Interval = TimeSpan.FromHours(1),
            }
        );

        await purger.StartAsync(TestContext.Current.CancellationToken);
        await purger.StopAsync(TestContext.Current.CancellationToken);

        purger.ExecuteTask!.IsCompleted.Should().BeTrue();
        purger.ExecuteTask.IsFaulted.Should().BeFalse();
        await deletedAccounts
            .DidNotReceiveWithAnyArgs()
            .PurgeDeletedUpToAsync(default, TestContext.Current.CancellationToken);
        logger.Entries.Should().BeEmpty();
    }
}
