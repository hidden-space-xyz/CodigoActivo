using AwesomeAssertions;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class DeletedAccountPurgerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private DeletedAccountPurger Build()
    {
        return new DeletedAccountPurger(
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Factory.Clock,
            new DeletedAccountPurgeOptions(),
            NullLogger<DeletedAccountPurger>.Instance
        );
    }

    private Task<List<Guid>> RemainingCopyIdsAsync()
    {
        return Factory.QueryAsync(db => db.DeletedAccounts.Select(copy => copy.Id).ToListAsync(Ct));
    }

    [Fact]
    public async Task PurgeAsyncKeepsACopyForTwoYearsAndDeletesItRightAfter()
    {
        var erasedToday = Guid.NewGuid();
        var erasedLongAgo = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.DeletedAccounts.AddRange(
                new DeletedAccount { Id = erasedToday, DeletedAt = Factory.Clock.UtcNow },
                new DeletedAccount
                {
                    Id = erasedLongAgo,
                    DeletedAt = Factory.Clock.UtcNow.AddYears(-3),
                }
            );
            return Task.CompletedTask;
        });

        (await Build().PurgeAsync(Ct)).Should().Be(1);
        (await RemainingCopyIdsAsync()).Should().Equal(erasedToday);

        Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddYears(2).AddSeconds(-1);
        (await Build().PurgeAsync(Ct)).Should().Be(0);
        (await RemainingCopyIdsAsync()).Should().Equal(erasedToday);

        Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddSeconds(1);
        (await Build().PurgeAsync(Ct)).Should().Be(1);
        (await RemainingCopyIdsAsync()).Should().BeEmpty();
    }
}
