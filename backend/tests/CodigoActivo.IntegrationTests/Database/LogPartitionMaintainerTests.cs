using AwesomeAssertions;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class LogPartitionMaintainerTests(PostgresDatabaseFixture postgres)
    : IClassFixture<PostgresDatabaseFixture>,
        IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly LogDatabase database = new(postgres.ConnectionString);
    private readonly TestClock clock = new() { UtcNow = Now };

    private LogPartitionMaintainer Maintainer => new(database, new LogRetentionOptions(), clock);

    public async ValueTask InitializeAsync()
    {
        await LogTables.TruncateAsync(postgres);
    }

    public async ValueTask DisposeAsync()
    {
        await database.DisposeAsync();
    }

    private async Task InsertDiagnosticAsync(DateTimeOffset occurredAt)
    {
        await using var db = postgres.CreateContext();
        db.Add(
            new DiagnosticLogEntry
            {
                Id = Guid.CreateVersion7(occurredAt),
                OccurredAt = occurredAt,
                Level = 3,
                Category = "Test",
                Message = "entry",
            }
        );
        await db.SaveChangesAsync(Ct);
    }

    [Theory]
    [InlineData("diagnostic_log_entries")]
    [InlineData("audit_log_entries")]
    public async Task LogTablesArePartitionedByRangeOfTheirInstant(string table)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT partitioned.partstrat::text, attribute.attname
            FROM pg_partitioned_table partitioned
            JOIN pg_class parent ON parent.oid = partitioned.partrelid
            JOIN pg_attribute attribute
                ON attribute.attrelid = parent.oid AND attribute.attnum = partitioned.partattrs[0]
            WHERE parent.relname = @table
            """,
            connection
        );
        command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync(Ct);

        (await reader.ReadAsync(Ct)).Should().BeTrue();
        reader.GetString(0).Should().Be("r");
        reader.GetString(1).Should().Be("occurred_at");
    }

    [Fact]
    public async Task MaintainAsyncCreatesTodayAndTheNextTwoDaysOfEveryTable()
    {
        await Maintainer.MaintainAsync(Ct);

        foreach (var table in LogTable.All)
        {
            (await LogTables.PartitionsAsync(postgres, table))
                .Should()
                .Contain([
                    $"{table.Name}_p20260310",
                    $"{table.Name}_p20260311",
                    $"{table.Name}_p20260312",
                ]);
        }
    }

    [Fact]
    public async Task MaintainAsyncDropsExpiredDaysAndTrimsTheDayOfTheCutoff()
    {
        await LogTables.CreatePartitionAsync(
            postgres,
            LogTable.Diagnostics,
            new DateOnly(2026, 2, 7)
        );
        await LogTables.CreatePartitionAsync(
            postgres,
            LogTable.Diagnostics,
            new DateOnly(2026, 2, 8)
        );
        await LogTables.CreatePartitionAsync(postgres, LogTable.Audit, new DateOnly(2026, 1, 8));
        await LogTables.CreatePartitionAsync(postgres, LogTable.Audit, new DateOnly(2026, 1, 9));
        await InsertDiagnosticAsync(new DateTimeOffset(2026, 2, 7, 10, 0, 0, TimeSpan.Zero));
        await InsertDiagnosticAsync(new DateTimeOffset(2026, 2, 8, 12, 59, 0, TimeSpan.Zero));
        var kept = new DateTimeOffset(2026, 2, 8, 13, 1, 0, TimeSpan.Zero);
        await InsertDiagnosticAsync(kept);

        await Maintainer.MaintainAsync(Ct);

        var diagnostics = await LogTables.PartitionsAsync(postgres, LogTable.Diagnostics);
        diagnostics.Should().NotContain("diagnostic_log_entries_p20260207");
        diagnostics.Should().Contain("diagnostic_log_entries_p20260208");
        var audit = await LogTables.PartitionsAsync(postgres, LogTable.Audit);
        audit.Should().NotContain("audit_log_entries_p20260108");
        audit.Should().Contain("audit_log_entries_p20260109");
        (await LogTables.DiagnosticEntriesAsync(postgres))
            .Should()
            .ContainSingle()
            .Which.OccurredAt.Should()
            .Be(kept);
    }

    [Fact]
    public async Task MaintainAsyncConfiguredRetentionDecidesWhatIsDropped()
    {
        var maintainer = new LogPartitionMaintainer(
            database,
            new LogRetentionOptions
            {
                Diagnostics = TimeSpan.FromDays(7),
                Audit = TimeSpan.FromDays(14),
            },
            clock
        );
        await LogTables.CreatePartitionAsync(
            postgres,
            LogTable.Diagnostics,
            new DateOnly(2026, 3, 2)
        );
        await LogTables.CreatePartitionAsync(
            postgres,
            LogTable.Diagnostics,
            new DateOnly(2026, 3, 3)
        );
        await LogTables.CreatePartitionAsync(postgres, LogTable.Audit, new DateOnly(2026, 2, 23));
        await LogTables.CreatePartitionAsync(postgres, LogTable.Audit, new DateOnly(2026, 2, 24));
        await InsertDiagnosticAsync(new DateTimeOffset(2026, 3, 3, 12, 59, 0, TimeSpan.Zero));
        var kept = new DateTimeOffset(2026, 3, 3, 13, 1, 0, TimeSpan.Zero);
        await InsertDiagnosticAsync(kept);

        await maintainer.MaintainAsync(Ct);

        var diagnostics = await LogTables.PartitionsAsync(postgres, LogTable.Diagnostics);
        diagnostics.Should().NotContain("diagnostic_log_entries_p20260302");
        diagnostics.Should().Contain("diagnostic_log_entries_p20260303");
        var audit = await LogTables.PartitionsAsync(postgres, LogTable.Audit);
        audit.Should().NotContain("audit_log_entries_p20260223");
        audit.Should().Contain("audit_log_entries_p20260224");
        (await LogTables.DiagnosticEntriesAsync(postgres))
            .Should()
            .ContainSingle()
            .Which.OccurredAt.Should()
            .Be(kept);
    }

    [Fact]
    public async Task EnsurePartitionsAsyncCreatesOnlyTheDaysInsideTheWindow()
    {
        await LogTables.DropPartitionAsync(
            postgres,
            LogTable.Diagnostics,
            new DateOnly(2026, 3, 1)
        );
        await using var connection = await database.OpenConnectionAsync(Ct);

        var outside = await Maintainer.EnsurePartitionsAsync(
            connection,
            LogTable.Diagnostics,
            [new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 20)],
            Ct
        );
        var inside = await Maintainer.EnsurePartitionsAsync(
            connection,
            LogTable.Diagnostics,
            [new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1)],
            Ct
        );

        outside.Should().BeFalse();
        inside.Should().BeTrue();
        var partitions = await LogTables.PartitionsAsync(postgres, LogTable.Diagnostics);
        partitions.Should().Contain("diagnostic_log_entries_p20260301");
        partitions.Should().NotContain("diagnostic_log_entries_p20260101");
        partitions.Should().NotContain("diagnostic_log_entries_p20260320");
    }

    [Fact]
    public async Task EnsurePartitionsAsyncConcurrentCallersCreateTheMissingDayOnce()
    {
        var day = new DateOnly(2026, 3, 2);
        await LogTables.DropPartitionAsync(postgres, LogTable.Diagnostics, day);

        var created = await Task.WhenAll(
            Enumerable
                .Range(0, 3)
                .Select(async _ =>
                {
                    await using var connection = await database.OpenConnectionAsync(Ct);
                    return await Maintainer.EnsurePartitionsAsync(
                        connection,
                        LogTable.Diagnostics,
                        [day],
                        Ct
                    );
                })
        );

        created.Should().AllSatisfy(inside => inside.Should().BeTrue());
        (await LogTables.PartitionsAsync(postgres, LogTable.Diagnostics))
            .Should()
            .Contain("diagnostic_log_entries_p20260302");
    }

    [Fact]
    public async Task TryMaintainAsyncRecordsAFailureInsteadOfThrowingIt()
    {
        await using var unreachable = new LogDatabase("Host=127.0.0.1;Port=1;Database=unused");
        var logs = new LogRecorder();

        await new LogPartitionMaintainer(
            unreachable,
            new LogRetentionOptions(),
            clock
        ).TryMaintainAsync(logs.CreateLogger("Maintenance"), Ct);

        logs.Entries.Should()
            .Equal("Error Maintenance: The log table maintenance failed; the next run will retry");
    }
}
