using AwesomeAssertions;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Validation;

public sealed class MessageValidatorTests
{
    private readonly MessageValidator sut = new(new TestClock());

    public static TheoryData<string> BlankCodes()
    {
        return [string.Empty, "   "];
    }

    [Theory]
    [MemberData(nameof(BlankCodes))]
    public void IsValidCompleteLoginBlankCodeReturnsFalse(string code)
    {
        sut.IsValid(
                new CompleteLoginCommand(
                    new LoginChallengeKey(UserId.New(), LoginChallengeId.New()),
                    code
                )
            )
            .Should()
            .BeFalse();
    }

    [Theory]
    [MemberData(nameof(BlankCodes))]
    public void IsValidVerifyUserBlankCodeReturnsFalse(string code)
    {
        sut.IsValid(new VerifyUserCommand(UserId.New(), code)).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(BlankCodes))]
    public void IsValidConfirmEmailChangeBlankCodeReturnsFalse(string code)
    {
        sut.IsValid(new ConfirmEmailChangeCommand(UserId.New(), code)).Should().BeFalse();
    }

    [Fact]
    public void IsValidCompleteLoginPresentCodeReturnsTrue()
    {
        sut.IsValid(
                new CompleteLoginCommand(
                    new LoginChallengeKey(UserId.New(), LoginChallengeId.New()),
                    "123456"
                )
            )
            .Should()
            .BeTrue();
    }
}
