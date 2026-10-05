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

        var erasure = AccountErasure.For(dependent, guardianId, Now);

        erasure.Should().Be(new AccountErasure(AccountDeletionOrigin.Guardian, guardianId, Now));
    }

    [Fact]
    public void ErasureForSomeoneElseErasingTheAccountIsAdministrator()
    {
        var administratorId = Guid.NewGuid();

        var ofAdult = AccountErasure.For(Account(), administratorId, Now);
        var ofDependent = AccountErasure.For(Account(Guid.NewGuid()), administratorId, Now);

        ofAdult
            .Should()
            .Be(new AccountErasure(AccountDeletionOrigin.Administrator, administratorId, Now));
        ofDependent.Should().Be(ofAdult);
    }

    [Fact]
    public void ErasureForClaimedEmailNamesTheClaimant()
    {
        var claimantId = Guid.NewGuid();

        var erasure = AccountErasure.ForClaimedEmail(claimantId, Now);

        erasure
            .Should()
            .Be(new AccountErasure(AccountDeletionOrigin.EmailClaimed, claimantId, Now));
    }

    [Fact]
    public void ErasureForMissingAccountThrows()
    {
        var act = () => AccountErasure.For(null!, Guid.NewGuid(), Now);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InitialAdministratorIdIsTheSeededAdministrator()
    {
        InitialAdministrator.Id.Should().Be(SeedIds.Users.InitialAdministrator);
    }

    [Fact]
    public void EnsureMayBeDeletedInitialAdministratorIsForbidden()
    {
        InitialAdministrator
            .EnsureMayBeDeleted(SeedIds.Users.InitialAdministrator)
            .ShouldFail(ErrorKind.Forbidden, ErrorCode.UserDeleteInitialAdminForbidden);
    }

    [Fact]
    public void EnsureMayBeDeletedAnyOtherAccountIsAllowed()
    {
        InitialAdministrator.EnsureMayBeDeleted(Guid.NewGuid()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void EnsureMayLoseAdminRightsInitialAdministratorIsForbidden()
    {
        InitialAdministrator
            .EnsureMayLoseAdminRights(SeedIds.Users.InitialAdministrator)
            .ShouldFail(ErrorKind.Forbidden, ErrorCode.UserCannotRemoveInitialAdmin);
    }

    [Fact]
    public void EnsureMayLoseAdminRightsAnyOtherAccountIsAllowed()
    {
        InitialAdministrator.EnsureMayLoseAdminRights(Guid.NewGuid()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RecordKeepsTheAccountIdTheErasureTimeAndTheLegalCopy()
    {
        var accountId = Guid.NewGuid();
        var erasure = new AccountErasure(AccountDeletionOrigin.Self, accountId, Now);

        var copy = DeletedAccount.Record(accountId, erasure, "{\"schemaVersion\":1}");

        copy.Id.Should().Be(accountId);
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
        var erasure = new AccountErasure(AccountDeletionOrigin.Self, accountId, Now);

        var act = () => DeletedAccount.Record(accountId, erasure, legalCopy!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordMissingErasureThrows()
    {
        var act = () => DeletedAccount.Record(Guid.NewGuid(), null!, "{}");

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
