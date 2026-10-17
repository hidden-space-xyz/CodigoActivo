using System.Diagnostics;
using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class AuditLogSinkTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly LogDatabase database = new(postgres.ConnectionString);
    private readonly TestClock clock = new() { UtcNow = Now };

    public async ValueTask InitializeAsync()
    {
        await LogTables.TruncateAsync(postgres);
        await LogTables.CreatePartitionAsync(postgres, LogTable.Audit, new DateOnly(2026, 7, 4));
    }

    public async ValueTask DisposeAsync()
    {
        await database.DisposeAsync();
    }

    private static AuditRecord Record(Guid actor, Guid target, Guid related)
    {
        return new AuditRecord(
            AuditAction.SetAdmin,
            AuditActorType.User,
            UserId.From(actor),
            IPAddress.Parse("203.0.113.9"),
            new AuditEntity(AuditEntityType.User, target),
            [new AuditEntity(AuditEntityType.Activity, related)],
            [new AuditDetail("IsAdmin", true), new AuditDetail("Status", "Confirmed")],
            AuditOutcome.Rejected,
            "UserCurrentPasswordIncorrect",
            ItemCount: 2
        );
    }

    [Fact]
    public async Task EmitBatchAsyncStoresEveryColumnOfTheRecord()
    {
        var actor = Guid.NewGuid();
        var target = Guid.NewGuid();
        var related = Guid.NewGuid();
        using var activity = new Activity("request").Start();
        var sink = new AuditLogSink(database, new LogPartitionMaintainer(database, clock));

        await sink.EmitBatchAsync([
            AuditLogEvents.ToEvent(Record(actor, target, related), Now, activity),
        ]);

        var entry = (await LogTables.AuditEntriesAsync(postgres)).Should().ContainSingle().Subject;
        entry.OccurredAt.Should().Be(Now);
        entry.ActorType.Should().Be("User");
        entry.ActorId.Should().Be(actor);
        entry.ClientIp.Should().Be(IPAddress.Parse("203.0.113.9"));
        entry.Action.Should().Be("SetAdmin");
        entry.EntityType.Should().Be("User");
        entry.EntityId.Should().Be(target);
        entry.Related.Should().Be($$"""[{"id": "{{related}}", "type": "Activity"}]""");
        entry.Details.Should().Be("""{"Status": "Confirmed", "IsAdmin": true}""");
        entry.Outcome.Should().Be("Rejected");
        entry.ErrorCode.Should().Be("UserCurrentPasswordIncorrect");
        entry.ItemCount.Should().Be(2);
        entry.TraceId.Should().Be(activity.TraceId.ToHexString());
    }

    [Fact]
    public async Task SerilogAuditTrailStoresWhatItQueuedWhenDisposed()
    {
        var target = Guid.NewGuid();

        await using (
            var trail = new SerilogAuditTrail(
                database,
                new LogPartitionMaintainer(database, clock),
                clock
            )
        )
        {
            trail.Record(
                new AuditRecord(
                    AuditAction.Login,
                    AuditActorType.Anonymous,
                    ActorId: null,
                    ClientAddress: null,
                    new AuditEntity(AuditEntityType.User, target),
                    [],
                    [],
                    AuditOutcome.Succeeded,
                    ErrorCode: null,
                    ItemCount: null
                )
            );
        }

        var entry = (await LogTables.AuditEntriesAsync(postgres)).Should().ContainSingle().Subject;
        entry.OccurredAt.Should().Be(Now);
        entry.ActorType.Should().Be("Anonymous");
        entry.ActorId.Should().BeNull();
        entry.ClientIp.Should().BeNull();
        entry.EntityId.Should().Be(target);
        entry.Related.Should().BeNull();
        entry.Details.Should().BeNull();
        entry.TraceId.Should().BeNull();
    }
}
