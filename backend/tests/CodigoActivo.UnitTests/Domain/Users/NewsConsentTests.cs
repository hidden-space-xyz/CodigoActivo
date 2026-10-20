using AwesomeAssertions;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.Users;

public sealed class NewsConsentTests
{
    private static readonly DateOnly Today = new(2026, 7, 4);
    private static readonly DateTimeOffset Created = new(2025, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private static PersonDetails Details(bool newsConsent, string email = "ana@test.com")
    {
        return new(
            "Ana",
            "Ruiz",
            Gender.Female,
            email,
            "600111222",
            null,
            "12345678Z",
            newsConsent
        );
    }

    private static User Account(bool newsConsent)
    {
        var account = User.CreateIndependent(
            Details(newsConsent),
            declaresAdult: true,
            Created
        ).Value;
        account.PullDomainEvents();
        return account;
    }

    private static void ApplyProfile(User account, PersonDetails details)
    {
        account.ApplyProfileChange(account.PlanProfileChange(details, null, Today).Value, Now);
    }

    [Fact]
    public void CreateIndependentWithConsentRecordsItAsGivenAtRegistration()
    {
        var account = Account(newsConsent: true);

        account.NewsConsent.Should().BeTrue();
        account
            .NewsConsentChanges.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    UserId = account.Id,
                    Granted = true,
                    Source = NewsConsentSource.Registration,
                    ChangedAt = Created,
                }
            );
    }

    [Fact]
    public void CreateIndependentWithoutConsentRecordsNoChange()
    {
        Account(newsConsent: false).NewsConsentChanges.Should().BeEmpty();
    }

    [Fact]
    public void CreateDependentAskedForConsentNeverHasIt()
    {
        var child = User.CreateDependent(
            Account(newsConsent: true),
            Details(newsConsent: true) with
            {
                Email = null,
                Phone = null,
                NationalId = null,
                BirthDate = new DateOnly(2016, 7, 4),
            },
            declaresGuardianship: true,
            Today,
            Created
        ).Value;

        child.NewsConsent.Should().BeFalse();
        child.NewsConsentChanges.Should().BeEmpty();
    }

    [Fact]
    public void ApplyProfileChangeWithdrawingRecordsItAsDecidedInTheProfile()
    {
        var account = Account(newsConsent: true);

        ApplyProfile(account, Details(newsConsent: false));

        account.NewsConsent.Should().BeFalse();
        account
            .NewsConsentChanges.Select(change => (change.Granted, change.Source, change.ChangedAt))
            .Should()
            .Equal(
                (true, NewsConsentSource.Registration, Created),
                (false, NewsConsentSource.Profile, Now)
            );
    }

    [Fact]
    public void ApplyProfileChangeKeepingTheConsentRecordsNothing()
    {
        var account = Account(newsConsent: true);

        ApplyProfile(account, Details(newsConsent: true) with { FirstName = "Anabel" });

        account.NewsConsentChanges.Should().ContainSingle();
    }

    [Fact]
    public void ApplyProfileChangeConfirmingEmailRecordsTheConsentAtOnce()
    {
        var account = Account(newsConsent: false);
        var change = account
            .PlanProfileChange(Details(newsConsent: true, email: "new@test.com"), null, Today)
            .Value;

        account.ApplyProfileChangeConfirmingEmail(change, "hash", Now, TimeSpan.FromMinutes(15));

        account.NewsConsent.Should().BeTrue();
        account
            .NewsConsentChanges.Should()
            .ContainSingle()
            .Which.Source.Should()
            .Be(NewsConsentSource.Profile);
    }

    [Theory]
    [InlineData(NewsConsentSource.UnsubscribeLink)]
    [InlineData(NewsConsentSource.OneClickUnsubscribe)]
    public void WithdrawNewsConsentRecordsItsSourceAndReportsTheChange(NewsConsentSource source)
    {
        var account = Account(newsConsent: true);

        account.WithdrawNewsConsent(source, Now);

        account.NewsConsent.Should().BeFalse();
        account.UpdatedAt.Should().Be(Now);
        var withdrawal = account.NewsConsentChanges.Last();
        withdrawal.Granted.Should().BeFalse();
        withdrawal.Source.Should().Be(source);
        withdrawal.ChangedAt.Should().Be(Now);
        DomainEvents
            .Raised<ProfileChanged>(account)
            .Should()
            .ContainSingle()
            .Which.UserId.Should()
            .Be(account.Id);
    }

    [Fact]
    public void WithdrawNewsConsentWithoutConsentChangesNothing()
    {
        var account = Account(newsConsent: false);

        account.WithdrawNewsConsent(NewsConsentSource.UnsubscribeLink, Now);

        account.NewsConsentChanges.Should().BeEmpty();
        account.UpdatedAt.Should().BeNull();
        account.PullDomainEvents().Should().BeEmpty();
    }

    [Theory]
    [InlineData(NewsConsentSource.Registration)]
    [InlineData(NewsConsentSource.Profile)]
    public void WithdrawNewsConsentFromAnotherSourceThrowsArgumentOutOfRange(
        NewsConsentSource source
    )
    {
        var account = Account(newsConsent: true);

        var withdraw = () => account.WithdrawNewsConsent(source, Now);

        withdraw.Should().Throw<ArgumentOutOfRangeException>();
        account.NewsConsent.Should().BeTrue();
    }
}
