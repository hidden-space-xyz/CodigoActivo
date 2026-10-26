using System.Diagnostics;
using System.Runtime.ExceptionServices;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Diagnostics;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.Infrastructure.Time;
using CodigoActivo.IntegrationTests.Infrastructure;
using Npgsql;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class DiagnosticLogSinkTests(PostgresDatabaseFixture postgres)
    : IClassFixture<PostgresDatabaseFixture>,
        IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly LogDatabase database = new(postgres.ConnectionString);
    private readonly TestClock clock = new() { UtcNow = Now };

    private DiagnosticLogSink Sink =>
        new(database, new LogPartitionMaintainer(database, new LogRetentionOptions(), clock));

    public async ValueTask InitializeAsync()
    {
        await LogTables.TruncateAsync(postgres);
        await LogTables.CreatePartitionAsync(
            postgres,
            LogTable.Diagnostics,
            new DateOnly(2026, 7, 4)
        );
    }

    public async ValueTask DisposeAsync()
    {
        await database.DisposeAsync();
    }

    private static LogEvent Event(DateTimeOffset occurredAt, string text)
    {
        return new LogEvent(
            occurredAt,
            LogEventLevel.Warning,
            exception: null,
            new MessageTemplateParser().Parse("Stored {Text}"),
            [
                new LogEventProperty("Text", new ScalarValue(text)),
                new LogEventProperty(
                    DiagnosticLogEvents.CategoryProperty,
                    new ScalarValue("Tests.Category")
                ),
            ]
        );
    }

    [Fact]
    public async Task EmitBatchAsyncStoresEveryEventOfTheBatch()
    {
        await Sink.EmitBatchAsync([
            Event(Now, "first\0"),
            Event(Now.AddMinutes(1), "second \uD800"),
        ]);

        var entries = await LogTables.DiagnosticEntriesAsync(postgres);
        entries.Select(entry => entry.Message).Should().Equal("Stored first", "Stored second �");
        entries[0].Category.Should().Be("Tests.Category");
        entries[0].Level.Should().Be((short)LogEventLevel.Warning);
        entries[0].Properties.Should().Be("""{"Text": "first"}""");
        entries[0].OccurredAt.Should().Be(Now);
    }

    [Fact]
    public async Task EmitBatchAsyncCreatesTheMissingPartitionOfARecentDayBeforeWriting()
    {
        var yesterday = new DateOnly(2026, 7, 3);
        await LogTables.DropPartitionAsync(postgres, LogTable.Diagnostics, yesterday);
        var failedCopies = 0;
        void CountFailedCopy(object? sender, FirstChanceExceptionEventArgs args)
        {
            if (args.Exception is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
            {
                Interlocked.Increment(ref failedCopies);
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += CountFailedCopy;
        try
        {
            await Sink.EmitBatchAsync([Event(Now.AddDays(-1), "late"), Event(Now, "today")]);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= CountFailedCopy;
        }

        failedCopies.Should().Be(0);
        (await LogTables.PartitionsAsync(postgres, LogTable.Diagnostics))
            .Should()
            .Contain(LogPartitions.NameOf(LogTable.Diagnostics, yesterday));
        (await LogTables.DiagnosticEntriesAsync(postgres))
            .Select(entry => entry.Message)
            .Should()
            .Equal("Stored late", "Stored today");
    }

    [Fact]
    public async Task EmitBatchAsyncRecreatesAPartitionDroppedAfterItsDayWasPrepared()
    {
        var sink = Sink;
        await sink.EmitBatchAsync([Event(Now, "first")]);
        await LogTables.DropPartitionAsync(
            postgres,
            LogTable.Diagnostics,
            new DateOnly(2026, 7, 4)
        );

        await sink.EmitBatchAsync([Event(Now.AddMinutes(1), "second")]);

        (await LogTables.DiagnosticEntriesAsync(postgres))
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Be("Stored second");
    }

    [Fact]
    public async Task EmitBatchAsyncDropsOnlyTheRowsTheDatabaseRefuses()
    {
        var expired = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await LogTables.DropPartitionAsync(
            postgres,
            LogTable.Diagnostics,
            new DateOnly(2026, 1, 1)
        );

        await Sink.EmitBatchAsync([Event(expired, "expired"), Event(Now, "kept")]);

        (await LogTables.DiagnosticEntriesAsync(postgres))
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Be("Stored kept");
        (await LogTables.PartitionsAsync(postgres, LogTable.Diagnostics))
            .Should()
            .NotContain(LogPartitions.NameOf(LogTable.Diagnostics, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public async Task EmitBatchAsyncThrowsWhenTheDatabaseIsUnreachableSoTheBatchIsRetried()
    {
        await using var unreachable = new LogDatabase("Host=127.0.0.1;Port=1;Database=unused");
        var sink = new DiagnosticLogSink(
            unreachable,
            new LogPartitionMaintainer(unreachable, new LogRetentionOptions(), clock)
        );

        var emit = () => sink.EmitBatchAsync([Event(Now, "lost")]);

        await emit.Should().ThrowAsync<NpgsqlException>();
    }

    [Fact]
    public async Task DatabaseLoggerProviderStoresTheEventsItQueuedWhenDisposed()
    {
        var systemClock = new SystemClock(TimeZoneInfo.Utc);
        await using (var connection = await database.OpenConnectionAsync(Ct))
        {
            await new LogPartitionMaintainer(
                database,
                new LogRetentionOptions(),
                systemClock
            ).EnsurePartitionsAsync(
                connection,
                LogTable.Diagnostics,
                [LogPartitions.DayOf(systemClock.UtcNow)],
                Ct
            );
        }

        using var activity = new Activity("request").Start();
        await using (
            var provider = new DatabaseLoggerProvider(
                database,
                new LogPartitionMaintainer(database, new LogRetentionOptions(), systemClock)
            )
        )
        {
            provider
                .CreateLogger("Tests.Pipeline")
                .EmailDeliveryAttemptFailed(
                    2,
                    EmailKind.PasswordReset,
                    new InvalidOperationException("boom")
                );
        }

        var entry = (await LogTables.DiagnosticEntriesAsync(postgres))
            .Should()
            .ContainSingle()
            .Subject;
        entry.Category.Should().Be("Tests.Pipeline");
        entry.EventName.Should().Be(nameof(InfrastructureLog.EmailDeliveryAttemptFailed));
        entry.Level.Should().Be((short)LogEventLevel.Warning);
        entry.TraceId.Should().Be(activity.TraceId.ToHexString());
        entry.Exception.Should().StartWith("System.InvalidOperationException: boom");
    }
}
