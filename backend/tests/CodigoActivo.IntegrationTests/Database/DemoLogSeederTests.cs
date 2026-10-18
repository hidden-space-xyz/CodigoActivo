using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class DemoLogSeederTests(
    CodigoActivoWebAppFactory factory,
    PostgresContainerFixture postgres
) : IntegrationTestBase(factory)
{
    private static readonly Guid[] DemoAdults =
    [
        .. Enumerable
            .Range(1, 3)
            .Select(index => new Guid($"dede0001-0000-0000-0000-00000000000{index}")),
    ];

    private Task SeedDemoAdultsAsync()
    {
        var now = Factory.Clock.UtcNow;
        return Factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                DemoAdults.Select(
                    (id, index) =>
                        Persisted.As<User>(
                            new
                            {
                                Id = id,
                                FirstName = "Demo",
                                LastName = $"Adult {index}",
                                Email = $"demo.adult.{index}@demo.codigoactivo.es",
                                Phone = $"+3466000000{index}",
                                NationalId = SpanishNationalId
                                    .FromDniNumber(30_000_000 + index)
                                    .Value,
                                Gender = Gender.Other,
                                Status = UserStatus.Active,
                                UserType = UserType.Member,
                                CreatedAt = now.AddDays(-90),
                                LastLoginAt = now.AddDays(-2),
                            }
                        )
                )
            );
            return Task.CompletedTask;
        });
    }

    private async Task SeedHistoryAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoLogSeeder>().SeedAsync(Ct);
    }

    private void AdvanceClock(TimeSpan by)
    {
        Factory.Clock.UtcNow += by;
        Factory.Clock.Today = DateOnly.FromDateTime(Factory.Clock.UtcNow.UtcDateTime);
    }

    [Fact]
    public async Task SeedAsyncWritesTheHistoryOnceWhileItIsRecent()
    {
        await SeedDemoAdultsAsync();

        await SeedHistoryAsync();
        var audit = await LogTables.AuditEntriesAsync(postgres);
        var diagnostics = await LogTables.DiagnosticEntriesAsync(postgres);
        await SeedHistoryAsync();

        audit
            .Should()
            .Contain(entry => entry.Action == "EndSession" && entry.ActorId == DemoAdults[0]);
        audit.Should().Contain(entry => entry.Action == "RemoveExpiredSessions");
        diagnostics.Should().Contain(entry => entry.EventName == "PasswordLockoutTriggered");
        (await LogTables.AuditEntriesAsync(postgres)).Should().HaveCount(audit.Count);
        (await LogTables.DiagnosticEntriesAsync(postgres)).Should().HaveCount(diagnostics.Count);
        (await LogTables.PartitionsAsync(postgres, LogTable.Audit))
            .Should()
            .Contain(LogPartitions.NameOf(LogTable.Audit, Factory.Clock.Today.AddDays(-12)));
    }

    [Fact]
    public async Task SeedAsyncWritesANewHistoryOnceThePreviousOneAged()
    {
        await SeedDemoAdultsAsync();
        await SeedHistoryAsync();
        var first = await LogTables.AuditEntriesAsync(postgres);

        AdvanceClock(LogRetentionOptions.DefaultDiagnostics);
        await SeedHistoryAsync();

        var entries = await LogTables.AuditEntriesAsync(postgres);
        entries.Should().HaveCountGreaterThan(first.Count);
        entries[^1]
            .OccurredAt.Should()
            .BeAfter(first[^1].OccurredAt.Add(LogRetentionOptions.DefaultDiagnostics / 2));
    }

    [Fact]
    public async Task SeedAsyncLeavesOutTheEntriesPastTheRetention()
    {
        await SeedDemoAdultsAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var retention = new LogRetentionOptions
        {
            Diagnostics = TimeSpan.FromDays(5),
            Audit = TimeSpan.FromDays(10),
        };
        var seeder = new DemoLogSeeder(
            services.GetRequiredService<CodigoActivoReadDbContext>(),
            services.GetRequiredService<LogDatabase>(),
            services.GetRequiredService<LogPartitionMaintainer>(),
            retention,
            new PasswordLockoutOptions(),
            new TwoFactorOptions(),
            services.GetRequiredService<IClock>(),
            services.GetRequiredService<ILogger<DemoLogSeeder>>()
        );

        await seeder.SeedAsync(Ct);

        var now = Factory.Clock.UtcNow;
        (await LogTables.DiagnosticEntriesAsync(postgres))
            .Should()
            .NotBeEmpty()
            .And.OnlyContain(entry =>
                entry.OccurredAt >= LogPartitions.CutoffOf(retention.Diagnostics, now)
            );
        (await LogTables.AuditEntriesAsync(postgres))
            .Should()
            .NotBeEmpty()
            .And.OnlyContain(entry =>
                entry.OccurredAt >= LogPartitions.CutoffOf(retention.Audit, now)
            );
        Factory.Logs.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsyncWithoutDemoAccountsWritesNothing()
    {
        await SeedHistoryAsync();

        (await LogTables.AuditEntriesAsync(postgres)).Should().BeEmpty();
        (await LogTables.DiagnosticEntriesAsync(postgres)).Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsyncUnreachableLogTablesAreLoggedWithoutThrowing()
    {
        await SeedDemoAdultsAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var unreachable = new LogDatabase(
            "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1"
        );
        var clock = services.GetRequiredService<IClock>();
        var retention = new LogRetentionOptions();
        var seeder = new DemoLogSeeder(
            services.GetRequiredService<CodigoActivoReadDbContext>(),
            unreachable,
            new LogPartitionMaintainer(unreachable, retention, clock),
            retention,
            new PasswordLockoutOptions(),
            new TwoFactorOptions(),
            clock,
            services.GetRequiredService<ILogger<DemoLogSeeder>>()
        );

        await seeder.SeedAsync(Ct);

        Factory
            .Logs.Entries.Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Warning CodigoActivo.Infrastructure.Database.Seeders.DemoLogSeeder:");
        (await LogTables.AuditEntriesAsync(postgres)).Should().BeEmpty();
    }
}
