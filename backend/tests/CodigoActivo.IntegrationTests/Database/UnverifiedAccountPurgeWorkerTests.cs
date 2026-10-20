using AwesomeAssertions;
using CodigoActivo.Domain.Users;
using CodigoActivo.Domain.Users.Erasure;
using CodigoActivo.Infrastructure.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class UnverifiedAccountPurgeWorkerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private UnverifiedAccountPurgeWorker Build()
    {
        return new UnverifiedAccountPurgeWorker(
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new UnverifiedAccountPurgeOptions(),
            NullLogger<UnverifiedAccountPurgeWorker>.Instance
        );
    }

    private Task<List<Guid>> UserIdsAsync()
    {
        return Factory.QueryAsync(db => db.Users.Select(user => user.Id.Value).ToListAsync(Ct));
    }

    private Task<List<Guid>> CopyIdsAsync()
    {
        return Factory.QueryAsync(db =>
            db.DeletedAccounts.Select(copy => copy.Id.Value).ToListAsync(Ct)
        );
    }

    [Fact]
    public async Task PurgeAsyncErasesAnAccountUnverifiedForTwentyFourHoursWithItsDependentsAndKeepsACopy()
    {
        var recentId = Guid.NewGuid();
        var recentChildId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                Persisted.As<User>(
                    new
                    {
                        Id = recentId,
                        FirstName = "Rita",
                        LastName = "Reciente",
                        Email = "rita.reciente@codigoactivo.test",
                        Phone = "+34600000099",
                        PasswordHash = "hash",
                        NationalId = "12345678Z",
                        Gender = Gender.Female,
                        Status = UserStatus.Pending,
                        UserType = UserType.Participant,
                        CreatedAt = Factory.Clock.UtcNow.AddDays(-1).AddSeconds(1),
                    }
                ),
                Persisted.As<User>(
                    new
                    {
                        Id = recentChildId,
                        FirstName = "Rosa",
                        LastName = "Reciente",
                        BirthDate = new DateOnly(2016, 5, 5),
                        Gender = Gender.Female,
                        ParentId = recentId,
                        Status = UserStatus.Dependent,
                        UserType = UserType.Participant,
                        CreatedAt = Factory.Clock.UtcNow.AddDays(-1).AddSeconds(1),
                    }
                )
            );
            return Task.CompletedTask;
        });

        (await Build().PurgeAsync(Ct)).Should().Be(1);
        (await UserIdsAsync()).Should().NotContain(TestSeedData.Users.PendingId);
        (await UserIdsAsync())
            .Should()
            .Contain([recentId, recentChildId, TestSeedData.Users.MemberId]);
        (await CopyIdsAsync()).Should().Equal(TestSeedData.Users.PendingId);

        Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddSeconds(1);
        (await Build().PurgeAsync(Ct)).Should().Be(1);
        (await UserIdsAsync()).Should().NotContain([recentId, recentChildId]);
        (await CopyIdsAsync()).Should().BeEquivalentTo([TestSeedData.Users.PendingId, recentId]);
        var copy = await Factory.QueryAsync(db =>
            db.DeletedAccounts.SingleAsync(deleted => deleted.Id == UserId.From(recentId), Ct)
        );
        copy.Data.Should().Contain(nameof(AccountDeletionOrigin.VerificationExpired));
        copy.Data.Should().Contain(recentChildId.ToString());
    }
}
