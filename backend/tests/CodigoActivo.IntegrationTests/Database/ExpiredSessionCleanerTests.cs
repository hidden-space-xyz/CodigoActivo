using AwesomeAssertions;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class ExpiredSessionCleanerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private ExpiredSessionCleaner Build()
    {
        return new ExpiredSessionCleaner(
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new SessionCleanupOptions(),
            NullLogger<ExpiredSessionCleaner>.Instance
        );
    }

    private Task SeedSessionAsync(Guid id, Guid userId, DateTimeOffset expiresAt)
    {
        return Factory.SeedAsync(db =>
        {
            db.Set<UserSession>()
                .Add(
                    Persisted.As<UserSession>(
                        new
                        {
                            Id = id,
                            UserId = userId,
                            CreatedAt = expiresAt.AddHours(-8),
                            ExpiresAt = expiresAt,
                        }
                    )
                );
            return Task.CompletedTask;
        });
    }

    private Task<List<Guid>> RemainingSessionIdsAsync()
    {
        return Factory.QueryAsync(db =>
            db.Set<UserSession>().Select(session => session.Id.Value).ToListAsync(Ct)
        );
    }

    [Fact]
    public async Task PurgeAsyncDeletesExpiredRowsAndKeepsLiveOnesOfEveryUser()
    {
        var live = Guid.NewGuid();
        var expired = Guid.NewGuid();
        var expiredExactlyNow = Guid.NewGuid();
        await SeedSessionAsync(live, TestSeedData.Users.MemberId, Factory.Clock.UtcNow.AddHours(1));
        await SeedSessionAsync(
            expired,
            TestSeedData.Users.MemberId,
            Factory.Clock.UtcNow.AddSeconds(-1)
        );
        await SeedSessionAsync(expiredExactlyNow, TestSeedData.Users.AdminId, Factory.Clock.UtcNow);

        var removed = await Build().PurgeAsync(Ct);

        removed.Should().Be(2);
        (await RemainingSessionIdsAsync()).Should().Equal(live);
    }

    [Fact]
    public async Task PurgeAsyncWithNothingExpiredRemovesNoRow()
    {
        var live = Guid.NewGuid();
        await SeedSessionAsync(live, TestSeedData.Users.MemberId, Factory.Clock.UtcNow.AddHours(1));

        var removed = await Build().PurgeAsync(Ct);

        removed.Should().Be(0);
        (await RemainingSessionIdsAsync()).Should().Equal(live);
    }

    [Fact]
    public async Task PurgeAsyncAfterTheClockMovesPastTheLifetimeClearsTheLoggedInSession()
    {
        await LoginAsMemberAsync();
        (await RemainingSessionIdsAsync()).Should().ContainSingle();

        Factory.Clock.UtcNow += SessionLifetimeOptions.DefaultLifetime + TimeSpan.FromMinutes(1);
        var removed = await Build().PurgeAsync(Ct);

        removed.Should().Be(1);
        (await RemainingSessionIdsAsync()).Should().BeEmpty();
    }
}
