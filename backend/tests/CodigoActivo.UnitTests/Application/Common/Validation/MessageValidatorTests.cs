using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.SecondFactor;
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

    private static RegisterCommand Register(string password = "password1234", int minors = 0)
    {
        return new RegisterCommand(
            "Ana",
            "Ruiz",
            "ana@test.com",
            "+34123456789",
            password,
            "12345678Z",
            Gender.Female,
            DeclaresAdult: true,
            NewsConsent: false,
            [
                .. Enumerable
                    .Range(0, minors)
                    .Select(_ => new MinorDraft(
                        "Leo",
                        "Ruiz",
                        new DateOnly(2020, 1, 1),
                        Gender.Other,
                        DeclaresGuardianship: true
                    )),
            ],
            SecondaryPhone: null
        );
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

    [Fact]
    public void IsValidRegisterCompleteCommandReturnsTrue()
    {
        sut.IsValid(Register(minors: Household.MaxDependents)).Should().BeTrue();
    }

    [Fact]
    public void IsValidRegisterBlankPasswordReturnsFalse()
    {
        sut.IsValid(Register(password: new string(' ', 12))).Should().BeFalse();
    }

    [Fact]
    public void IsValidRegisterMoreMinorsThanAHouseholdHoldsReturnsFalse()
    {
        sut.IsValid(Register(minors: Household.MaxDependents + 1)).Should().BeFalse();
    }
}
