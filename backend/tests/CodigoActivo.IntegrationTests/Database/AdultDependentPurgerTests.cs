using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class AdultDependentPurgerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private AdultDependentPurger Build()
    {
        return new AdultDependentPurger(
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new AdultDependentPurgeOptions(),
            NullLogger<AdultDependentPurger>.Instance
        );
    }

    private Task SeedDependentAsync(Guid id, string firstName, DateOnly birthDate)
    {
        return Factory.SeedAsync(db =>
        {
            db.Users.Add(
                Persisted.As<User>(
                    new
                    {
                        Id = id,
                        FirstName = firstName,
                        LastName = "Miembro",
                        BirthDate = birthDate,
                        Gender = Gender.Female,
                        ParentId = TestSeedData.Users.MemberId,
                        Status = UserStatus.Dependent,
                        UserType = UserType.Participant,
                        CreatedAt = Factory.Clock.UtcNow.AddYears(-1),
                    }
                )
            );
            return Task.CompletedTask;
        });
    }

    private Task<User?> FindAsync(Guid id)
    {
        return Factory.QueryAsync(db =>
            db.Users.SingleOrDefaultAsync(user => user.Id == UserId.From(id), Ct)
        );
    }

    private List<EmailMessage> Notices()
    {
        return
        [
            .. Factory.EmailSender.Sent.Where(message =>
                message.Kind is EmailKind.ComingOfAgeNotice
            ),
        ];
    }

    [Fact]
    public async Task PurgeAsyncErasesWhoCameOfAgeAndTellsTheGuardianOfWhoComesOfAgeSoonOnce()
    {
        var adultId = Guid.NewGuid();
        var soonId = Guid.NewGuid();
        await SeedDependentAsync(adultId, "Alicia", Factory.Clock.Today.AddYears(-18));
        await SeedDependentAsync(soonId, "Sara", new DateOnly(2008, 7, 20));

        (await Build().PurgeAsync(Ct)).Should().Be((1, 1));
        await Factory.DrainEmailOutboxAsync();

        (await FindAsync(adultId)).Should().BeNull();
        var copy = await Factory.QueryAsync(db =>
            db.DeletedAccounts.SingleAsync(deleted => deleted.Id == UserId.From(adultId), Ct)
        );
        copy.Data.Should().Contain(nameof(AccountDeletionOrigin.CameOfAge));
        copy.Data.Should().Contain(TestSeedData.Users.MemberId.ToString());
        var soon = await FindAsync(soonId);
        soon!.ComingOfAgeNoticeSentAt.Should().Be(Factory.Clock.UtcNow);
        (await FindAsync(TestSeedData.Users.MemberChildId)).Should().NotBeNull();
        var notice = Notices().Should().ContainSingle().Subject;
        notice.ToAddress.Should().Be(TestSeedData.MemberEmail);
        notice.TextBody.Should().Contain("Sara Miembro cumple 18 años el 20/07/2026");

        (await Build().PurgeAsync(Ct)).Should().Be((0, 0));
        await Factory.DrainEmailOutboxAsync();
        Notices().Should().ContainSingle();
    }
}
