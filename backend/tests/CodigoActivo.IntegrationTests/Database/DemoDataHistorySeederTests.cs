using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.DataHistory;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class DemoDataHistorySeederTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly Guid FormerGuardian = DemoDataSeeder.DemoUser(100).Value;
    private static readonly Guid FormerChild = DemoDataSeeder.DemoUser(101).Value;
    private static readonly Guid FormerSponsor = DemoDataSeeder.DemoUser(102).Value;
    private static readonly Guid Unverified = DemoDataSeeder.DemoUser(103).Value;

    private Task SeedDemoDataAsync()
    {
        return Factory.SeedAsync(async db =>
        {
            var administrator = await db.Users.SingleAsync(
                user => user.Id == InitialAdministrator.Id,
                Ct
            );
            var graph = DemoDataSeeder.BuildGraph(
                Factory.Clock,
                Factory.Services.GetRequiredService<IPasswordHasher>(),
                administrator
            );
            await DemoDataSeeder.AddAsync(db, graph, Ct);
        });
    }

    private async Task SeedHistoryAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoDataHistorySeeder>().SeedAsync(Ct);
    }

    private static string? Text(DataHistoryRecord record, string property)
    {
        return
            record.Values.TryGetValue(property, out var value)
            && value is JsonElement { ValueKind: JsonValueKind.String } text
            ? text.GetString()
            : null;
    }

    [Fact]
    public async Task SeedAsyncErasesTheFormerAccountsAsTheCommandsThatWouldHave()
    {
        await SeedDemoDataAsync();

        await SeedHistoryAsync();

        List<(Guid, string?, Guid?)> expected =
        [
            (FormerGuardian, "DeleteOwnAccount", FormerGuardian),
            (FormerChild, "DeleteOwnAccount", FormerGuardian),
            (FormerSponsor, "DeleteUser", InitialAdministrator.Id.Value),
            (Unverified, "PurgeUnverifiedAccounts", null),
        ];
        (await ErasedUsersAsync()).Should().BeEquivalentTo(expected);
        var remaining = await Factory.QueryAsync(db =>
            db.Users.CountAsync(
                user =>
                    user.Id == UserId.From(FormerGuardian)
                    || user.Id == UserId.From(FormerChild)
                    || user.Id == UserId.From(FormerSponsor)
                    || user.Id == UserId.From(Unverified),
                Ct
            )
        );
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task SeedAsyncKeepsWhatTheErasedHouseholdHadAboutBothPeople()
    {
        await SeedDemoDataAsync();

        await SeedHistoryAsync();

        var deleted = (await DataHistoryAsync())
            .Where(item =>
                item.Record.Operation is DataHistoryOperation.Deleted
                && item.Record.Command == "DeleteOwnAccount"
            )
            .ToList();
        deleted
            .Where(item => item.Entry.Entity == nameof(Assignment))
            .Should()
            .HaveCount(4)
            .And.OnlyContain(item =>
                item.Entry.SubjectIds.Contains(FormerGuardian)
                || item.Entry.SubjectIds.Contains(FormerChild)
            );
        deleted
            .Should()
            .ContainSingle(item => item.Entry.Entity == nameof(EventRating))
            .Which.Entry.SubjectIds.Should()
            .Equal(FormerGuardian);
        deleted
            .Should()
            .Contain(item => item.Entry.Entity == nameof(NewsConsentChange))
            .And.OnlyContain(item => item.Entry.OccurredAt == deleted[0].Entry.OccurredAt);
    }

    [Fact]
    public async Task SeedAsyncSpreadsTheChangesOverThePastAsTheirOwnCommands()
    {
        await SeedDemoDataAsync();

        await SeedHistoryAsync();

        var now = Factory.Clock.UtcNow;
        var history = await DataHistoryAsync();
        history
            .Should()
            .OnlyContain(item =>
                item.Entry.OccurredAt < now && item.Entry.OccurredAt > now.AddDays(-401)
            );
        history
            .Select(item => item.Entry.OccurredAt.Date)
            .Distinct()
            .Should()
            .HaveCountGreaterThan(10);
        history
            .Select(item => item.Record.Command)
            .Should()
            .Contain([
                "VerifyUser",
                "ChangeUserType",
                "ChangeAssignmentStatus",
                "UpdateUser",
                "ConfirmEmailChange",
                "Unsubscribe",
                "UnassignActivity",
                "DeleteEventRating",
            ]);
    }

    [Fact]
    public async Task SeedAsyncKeepsTheEmailAnExistingAccountReplaced()
    {
        await SeedDemoDataAsync();

        await SeedHistoryAsync();

        var replaced = (await DataHistoryAsync()).Single(item =>
            item.Record.Command == "ConfirmEmailChange"
        );
        var account = await FindAsync<User>(replaced.Entry.SubjectIds.Single());
        account.Should().NotBeNull();
        replaced.Record.ActorId.Should().Be(account!.Id.Value);
        Text(replaced.Record, nameof(User.Email))
            .Should()
            .NotBeNull()
            .And.NotBe(account.Email!.Value)
            .And.Be(account.Email.Value.Replace(".personal@", "@", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SeedAsyncReplaysTheHistoryOnlyOnce()
    {
        await SeedDemoDataAsync();
        await SeedHistoryAsync();
        var first = await DataHistoryAsync();

        await SeedHistoryAsync();

        (await DataHistoryAsync()).Should().HaveCount(first.Count);
    }
}
