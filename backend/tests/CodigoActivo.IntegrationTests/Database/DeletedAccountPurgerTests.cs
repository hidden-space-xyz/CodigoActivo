using AwesomeAssertions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Users;
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
            new DeletedAccountPurgeOptions(),
            NullLogger<DeletedAccountPurger>.Instance
        );
    }

    private Task<List<Guid>> RemainingCopyIdsAsync()
    {
        return Factory.QueryAsync(db =>
            db.DeletedAccounts.Select(copy => copy.Id.Value).ToListAsync(Ct)
        );
    }

    private Task<List<Guid>> RemainingParticipationCopyIdsAsync()
    {
        return Factory.QueryAsync(db =>
            db.DeletedParticipations.Select(copy => copy.Id.Value).ToListAsync(Ct)
        );
    }

    [Fact]
    public async Task PurgeAsyncKeepsACopyForTwoYearsAndDeletesItRightAfter()
    {
        var erasedToday = Guid.NewGuid();
        var erasedLongAgo = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.DeletedAccounts.AddRange(
                Persisted.As<DeletedAccount>(
                    new { Id = erasedToday, DeletedAt = Factory.Clock.UtcNow }
                ),
                Persisted.As<DeletedAccount>(
                    new { Id = erasedLongAgo, DeletedAt = Factory.Clock.UtcNow.AddYears(-3) }
                )
            );
            return Task.CompletedTask;
        });

        (await Build().PurgeAsync(Ct)).Should().Be((1, 0));
        (await RemainingCopyIdsAsync()).Should().Equal(erasedToday);

        Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddYears(2).AddSeconds(-1);
        (await Build().PurgeAsync(Ct)).Should().Be((0, 0));
        (await RemainingCopyIdsAsync()).Should().Equal(erasedToday);

        Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddSeconds(1);
        (await Build().PurgeAsync(Ct)).Should().Be((1, 0));
        (await RemainingCopyIdsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PurgeAsyncKeepsAParticipationCopyForTwoYearsAndDeletesItRightAfter()
    {
        var removedToday = Guid.NewGuid();
        var removedLongAgo = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.DeletedParticipations.AddRange(
                Persisted.As<DeletedParticipation>(
                    new { Id = removedToday, DeletedAt = Factory.Clock.UtcNow }
                ),
                Persisted.As<DeletedParticipation>(
                    new { Id = removedLongAgo, DeletedAt = Factory.Clock.UtcNow.AddYears(-3) }
                )
            );
            return Task.CompletedTask;
        });

        (await Build().PurgeAsync(Ct)).Should().Be((0, 1));
        (await RemainingParticipationCopyIdsAsync()).Should().Equal(removedToday);

        Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddYears(2).AddSeconds(-1);
        (await Build().PurgeAsync(Ct)).Should().Be((0, 0));

        Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddSeconds(1);
        (await Build().PurgeAsync(Ct)).Should().Be((0, 1));
        (await RemainingParticipationCopyIdsAsync()).Should().BeEmpty();
    }
}
