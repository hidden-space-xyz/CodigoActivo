using AwesomeAssertions;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Domain.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class DataHistoryTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly Guid MemberId = TestSeedData.Users.MemberId;

    private Task RenameMemberAsync(string firstName)
    {
        return Factory.SeedAsync(async db =>
        {
            var member = await db.Users.SingleAsync(user => user.Id == UserId.From(MemberId), Ct);
            Persisted.Overwrite(member, new { FirstName = firstName });
        });
    }

    [Fact]
    public async Task ReadAboutAsyncPeriodKeepsOnlyTheChangesInsideIt()
    {
        var start = Factory.Clock.UtcNow;
        await RenameMemberAsync("Uno");
        Factory.Clock.UtcNow = start.AddHours(1);
        await RenameMemberAsync("Dos");
        Factory.Clock.UtcNow = start.AddHours(2);
        await RenameMemberAsync("Tres");
        await using var scope = Factory.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IDataHistoryReader>();

        var items = await reader.ReadAboutAsync(
            [MemberId],
            new DataHistoryPeriod(start.AddHours(1), start.AddHours(2), [], start.AddHours(3)),
            Ct
        );

        items.Should().ContainSingle().Which.OccurredAt.Should().Be(start.AddHours(1));
    }

    [Fact]
    public async Task SaveChangesAsyncChangingOrDeletingAHistoryEntryIsRefused()
    {
        await RenameMemberAsync("Uno");

        var change = () =>
            Factory.SeedAsync(async db =>
            {
                var entry = await db.DataHistory.SingleAsync(Ct);
                db.Entry(entry).State = EntityState.Modified;
            });
        var delete = () =>
            Factory.SeedAsync(async db =>
            {
                db.DataHistory.Remove(await db.DataHistory.SingleAsync(Ct));
            });

        await change.Should().ThrowAsync<InvalidOperationException>();
        await delete.Should().ThrowAsync<InvalidOperationException>();
        (await Factory.QueryAsync(db => db.DataHistory.CountAsync(Ct))).Should().Be(1);
    }
}
