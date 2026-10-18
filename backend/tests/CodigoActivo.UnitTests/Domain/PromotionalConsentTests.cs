using AwesomeAssertions;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class PromotionalConsentTests
{
    private static readonly DateOnly Today = new(2026, 7, 4);
    private static readonly DateTimeOffset Created = new(2025, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private static PersonDetails Details(bool promotionalConsent, string email = "ana@test.com")
    {
        return new(
            "Ana",
            "Ruiz",
            Gender.Female,
            email,
            "600111222",
            null,
            "12345678Z",
            promotionalConsent
        );
    }

    private static User Account(bool promotionalConsent)
    {
        var account = User.CreateIndependent(Details(promotionalConsent), Created).Value;
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
        var account = Account(promotionalConsent: true);

        account.PromotionalConsent.Should().BeTrue();
        account
            .PromotionalConsentChanges.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    UserId = account.Id,
                    Granted = true,
                    Source = PromotionalConsentSource.Registration,
                    ChangedAt = Created,
                }
            );
    }

    [Fact]
    public void CreateIndependentWithoutConsentRecordsNoChange()
    {
        Account(promotionalConsent: false).PromotionalConsentChanges.Should().BeEmpty();
    }

    [Fact]
    public void CreateDependentAskedForConsentNeverHasIt()
    {
        var child = User.CreateDependent(
            Account(promotionalConsent: true),
            Details(promotionalConsent: true) with
            {
                Email = null,
                Phone = null,
                NationalId = null,
                BirthDate = new DateOnly(2016, 7, 4),
            },
            Today,
            Created
        ).Value;

        child.PromotionalConsent.Should().BeFalse();
        child.PromotionalConsentChanges.Should().BeEmpty();
    }

    [Fact]
    public void ApplyProfileChangeWithdrawingRecordsItAsDecidedInTheProfile()
    {
        var account = Account(promotionalConsent: true);

        ApplyProfile(account, Details(promotionalConsent: false));

        account.PromotionalConsent.Should().BeFalse();
        account
            .PromotionalConsentChanges.Select(change =>
                (change.Granted, change.Source, change.ChangedAt)
            )
            .Should()
            .Equal(
                (true, PromotionalConsentSource.Registration, Created),
                (false, PromotionalConsentSource.Profile, Now)
            );
    }

    [Fact]
    public void ApplyProfileChangeKeepingTheConsentRecordsNothing()
    {
        var account = Account(promotionalConsent: true);

        ApplyProfile(account, Details(promotionalConsent: true) with { FirstName = "Anabel" });

        account.PromotionalConsentChanges.Should().ContainSingle();
    }

    [Fact]
    public void ApplyProfileChangeConfirmingEmailRecordsTheConsentAtOnce()
    {
        var account = Account(promotionalConsent: false);
        var change = account
            .PlanProfileChange(
                Details(promotionalConsent: true, email: "new@test.com"),
                null,
                Today
            )
            .Value;

        account.ApplyProfileChangeConfirmingEmail(change, "hash", Now, TimeSpan.FromMinutes(15));

        account.PromotionalConsent.Should().BeTrue();
        account
            .PromotionalConsentChanges.Should()
            .ContainSingle()
            .Which.Source.Should()
            .Be(PromotionalConsentSource.Profile);
    }

    [Fact]
    public void WithdrawPromotionalConsentRecordsTheUnsubscribeLinkAndReportsTheChange()
    {
        var account = Account(promotionalConsent: true);

        account.WithdrawPromotionalConsent(Now);

        account.PromotionalConsent.Should().BeFalse();
        account.UpdatedAt.Should().Be(Now);
        var withdrawal = account.PromotionalConsentChanges.Last();
        withdrawal.Granted.Should().BeFalse();
        withdrawal.Source.Should().Be(PromotionalConsentSource.UnsubscribeLink);
        withdrawal.ChangedAt.Should().Be(Now);
        DomainEvents
            .Raised<ProfileChanged>(account)
            .Should()
            .ContainSingle()
            .Which.UserId.Should()
            .Be(account.Id);
    }

    [Fact]
    public void WithdrawPromotionalConsentWithoutConsentChangesNothing()
    {
        var account = Account(promotionalConsent: false);

        account.WithdrawPromotionalConsent(Now);

        account.PromotionalConsentChanges.Should().BeEmpty();
        account.UpdatedAt.Should().BeNull();
        account.PullDomainEvents().Should().BeEmpty();
    }
}
