using AwesomeAssertions;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;
using StaffRole = CodigoActivo.Application.Abstractions.Querying.ReadModel.StaffRole;

namespace CodigoActivo.UnitTests.Application.BlockedData;

public sealed class PrivacyOfficerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly PrivacyOfficer sut;

    public PrivacyOfficerTests()
    {
        sut = new PrivacyOfficer(users, currentUser);
    }

    private User SignedIn(
        bool isPrivacyOfficer = true,
        StaffRole staffRole = StaffRole.Administrator,
        TwoFactorMethod method = TwoFactorMethod.Authenticator
    )
    {
        var user = NewUser(staffRole: staffRole, isPrivacyOfficer: isPrivacyOfficer);
        Persisted.Overwrite(user, new { TwoFactorMethod = method });
        currentUser.Id = user.Id;
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public void DesignatesOnlyTheActiveDesignatedAccount()
    {
        PrivacyOfficer.Designates(isPrivacyOfficer: true, isActive: true).Should().BeTrue();
        PrivacyOfficer.Designates(isPrivacyOfficer: false, isActive: true).Should().BeFalse();
        PrivacyOfficer.Designates(isPrivacyOfficer: true, isActive: false).Should().BeFalse();
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
        SignedIn(isPrivacyOfficer: false);

        var result = await sut.SignedInOfficerAsync(TestContext.Current.CancellationToken);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.BlockedDataOfficerOnly);
    }
}
