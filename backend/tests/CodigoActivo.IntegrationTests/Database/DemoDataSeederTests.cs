using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class DemoDataSeederTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string PendingName = "Rocío";

    private Task AddGraphAsync()
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

    private async Task SeedAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(Ct);
    }

    private Task<List<User>> PendingAccountsAsync()
    {
        return Factory.QueryAsync(db =>
            db.Users.Where(user => user.FirstName == PendingName).ToListAsync(Ct)
        );
    }

    [Fact]
    public async Task AddAsyncStoresTheTermsDecisions()
    {
        await AddGraphAsync();

        (await Factory.QueryAsync(db => db.EventTermsAcceptances.CountAsync(Ct)))
            .Should()
            .BePositive();
    }

    [Fact]
    public async Task SeedAsyncKeepsOneRegistrationWaitingForItsVerification()
    {
        await AddGraphAsync();

        await SeedAsync();
        await SeedAsync();

        var pending = await PendingAccountsAsync();
        pending.Should().ContainSingle().Which.Status.Should().Be(UserStatus.Pending);
        pending[0].CreatedAt.Should().BeBefore(Factory.Clock.UtcNow);
        pending[0].CreatedAt.Should().BeAfter(User.UnverifiedPurgeCutoff(Factory.Clock.UtcNow));
    }

    [Fact]
    public async Task SeedAsyncBringsTheRegistrationBackAfterThePurge()
    {
        await AddGraphAsync();
        await SeedAsync();
        await Factory.SeedAsync(async db =>
        {
            db.Users.RemoveRange(
                await db.Users.Where(user => user.FirstName == PendingName).ToListAsync(Ct)
            );
        });

        await SeedAsync();

        (await PendingAccountsAsync()).Should().ContainSingle();
    }
}
