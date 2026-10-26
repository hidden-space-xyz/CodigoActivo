using AwesomeAssertions;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.BlockedData;

public sealed class PrivacyOfficerTests
{
    private const string OfficerEmail = "dpd@test.com";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly PrivacyOfficer sut;

    public PrivacyOfficerTests()
    {
        sut = new PrivacyOfficer(
            new PrivacyOfficerOptions { Email = OfficerEmail },
            users,
            currentUser
        );
    }

    private User SignedIn(
        string email = OfficerEmail,
        bool isAdmin = true,
        Guid? statusId = null,
        TwoFactorMethod method = TwoFactorMethod.Authenticator
    )
    {
        var user = NewUser(email: email, isAdmin: isAdmin, statusId: statusId);
        Persisted.Overwrite(user, new { TwoFactorMethod = method });
        currentUser.Id = user.Id;
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public void DesignatesOnlyTheActiveAdministratorWithTheConfiguredEmail()
    {
        sut.Designates(isAdmin: true, isActive: true, OfficerEmail).Should().BeTrue();
        sut.Designates(isAdmin: false, isActive: true, OfficerEmail).Should().BeFalse();
        sut.Designates(isAdmin: true, isActive: false, OfficerEmail).Should().BeFalse();
        sut.Designates(isAdmin: true, isActive: true, "otra@test.com").Should().BeFalse();
        sut.Designates(isAdmin: true, isActive: true, null).Should().BeFalse();
    }

    [Theory]
    [InlineData(TwoFactorMethod.Email)]
    [InlineData(TwoFactorMethod.Authenticator)]
    public async Task SignedInOfficerAsyncDesignatedAccountWithAnySecondFactorReturnsIt(
        TwoFactorMethod method
    )
    {
        var user = SignedIn(method: method);

        var result = await sut.SignedInOfficerAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(user);
    }

    [Fact]
    public async Task SignedInOfficerAsyncAnotherAdministratorIsRefused()
    {
        SignedIn(email: "admin@test.com");

        var result = await sut.SignedInOfficerAsync(TestContext.Current.CancellationToken);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.BlockedDataOfficerOnly);
    }

    [Fact]
    public async Task SignedInOfficerAsyncDesignatedEmailWithoutAdministratorRightsIsRefused()
    {
        SignedIn(isAdmin: false);

        var result = await sut.SignedInOfficerAsync(TestContext.Current.CancellationToken);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.BlockedDataOfficerOnly);
    }

    [Fact]
    public async Task SignedInOfficerAsyncBlockedAccountIsRefused()
    {
        SignedIn(statusId: KnownIds.UserStatusTypes.Blocked);

        var result = await sut.SignedInOfficerAsync(TestContext.Current.CancellationToken);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.BlockedDataOfficerOnly);
    }
}
