using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class AccountDeletionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private static User Account(Guid? guardianId = null)
    {
        return Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = "Ana",
                LastName = "Ruiz",
                ParentId = guardianId,
            }
        );
    }

    [Fact]
    public void ErasureForAccountErasingItselfIsSelf()
    {
        var account = Account();

        var erasure = AccountErasure.For(account, account.Id, Now);

        erasure.Should().Be(new AccountErasure(AccountDeletionOrigin.Self, account.Id, Now));
    }

    [Fact]
    public void ErasureForGuardianErasingTheirDependentIsGuardian()
    {
        var guardianId = Guid.NewGuid();
        var dependent = Account(guardianId);

        var erasure = AccountErasure.For(dependent, UserId.From(guardianId), Now);

        erasure
            .Should()
            .Be(new AccountErasure(AccountDeletionOrigin.Guardian, UserId.From(guardianId), Now));
    }

    [Fact]
    public void ErasureForSomeoneElseErasingTheAccountIsAdministrator()
    {
        var administratorId = Guid.NewGuid();

        var ofAdult = AccountErasure.For(Account(), UserId.From(administratorId), Now);
        var ofDependent = AccountErasure.For(
            Account(Guid.NewGuid()),
            UserId.From(administratorId),
            Now
        );

        ofAdult
            .Should()
            .Be(
                new AccountErasure(
                    AccountDeletionOrigin.Administrator,
                    UserId.From(administratorId),
                    Now
                )
            );
        ofDependent.Should().Be(ofAdult);
    }

    [Fact]
    public void ErasureForClaimedEmailNamesTheClaimant()
    {
        var claimantId = Guid.NewGuid();

        var erasure = AccountErasure.ForClaimedEmail(UserId.From(claimantId), Now);

        erasure
            .Should()
            .Be(
                new AccountErasure(AccountDeletionOrigin.EmailClaimed, UserId.From(claimantId), Now)
            );
    }

    [Fact]
    public void ErasureForMissingAccountThrows()
    {
        var act = () => AccountErasure.For(null!, UserId.From(Guid.NewGuid()), Now);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InitialAdministratorIdIsTheSeededAdministrator()
    {
        InitialAdministrator.Id.Value.Should().Be(KnownIds.Users.InitialAdministrator);
    }

    [Fact]
    public void EnsureMayBeDeletedInitialAdministratorIsForbidden()
    {
        InitialAdministrator
            .EnsureMayBeDeleted(UserId.From(KnownIds.Users.InitialAdministrator))
            .ShouldFail(ErrorKind.Forbidden, DomainErrorCode.UserDeleteInitialAdminForbidden);
    }

    [Fact]
    public void EnsureMayBeDeletedAnyOtherAccountIsAllowed()
    {
        InitialAdministrator
            .EnsureMayBeDeleted(UserId.From(Guid.NewGuid()))
            .IsSuccess.Should()
            .BeTrue();
    }

    [Fact]
    public void EnsureMayLoseAdminRightsInitialAdministratorIsForbidden()
    {
        InitialAdministrator
            .EnsureMayLoseAdminRights(UserId.From(KnownIds.Users.InitialAdministrator))
            .ShouldFail(ErrorKind.Forbidden, DomainErrorCode.UserCannotRemoveInitialAdmin);
    }

    [Fact]
    public void EnsureMayLoseAdminRightsAnyOtherAccountIsAllowed()
    {
        InitialAdministrator
            .EnsureMayLoseAdminRights(UserId.From(Guid.NewGuid()))
            .IsSuccess.Should()
            .BeTrue();
    }

    [Fact]
    public void RecordKeepsTheAccountIdTheErasureTimeAndTheLegalCopy()
    {
        var accountId = Guid.NewGuid();
        var erasure = new AccountErasure(AccountDeletionOrigin.Self, UserId.From(accountId), Now);

        var copy = DeletedAccount.Record(UserId.From(accountId), erasure, "{\"schemaVersion\":1}");

        copy.Id.Value.Should().Be(accountId);
        copy.DeletedAt.Should().Be(Now);
        copy.Data.Should().Be("{\"schemaVersion\":1}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordBlankLegalCopyThrows(string? legalCopy)
    {
        var accountId = Guid.NewGuid();
        var erasure = new AccountErasure(AccountDeletionOrigin.Self, UserId.From(accountId), Now);

        var act = () => DeletedAccount.Record(UserId.From(accountId), erasure, legalCopy!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordMissingErasureThrows()
    {
        var act = () => DeletedAccount.Record(UserId.From(Guid.NewGuid()), null!, "{}");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PurgeCutoffIsTheRetentionPeriodBeforeNow()
    {
        DeletedAccount.RetentionYears.Should().Be(2);
        DeletedAccount
            .PurgeCutoff(Now)
            .Should()
            .Be(new DateTimeOffset(2024, 7, 4, 12, 0, 0, TimeSpan.Zero));
    }
}
