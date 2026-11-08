using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class VerifyUserCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new();
    private readonly VerifyUserCommandHandler sut;

    public VerifyUserCommandHandlerTests()
    {
        sut = new VerifyUserCommandHandler(
            users,
            clock,
            new OtpValidator(new FakeOneTimeCodeHasher())
        );
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await sut.HandleAsync(
            new VerifyUserCommand(UserId.New(), "123456"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
    }

    [Fact]
    public async Task HandleAsyncUserNotPendingReturnsBadRequest()
    {
        var user = users.FindReturns(
            NewUser(
                statusId: KnownIds.UserStatusTypes.Active,
                otpCodeHash: FakeOneTimeCodeHasher.Prefix + "123456",
                otpExpiresAt: clock.UtcNow.AddMinutes(5)
            )
        );

        var result = await sut.HandleAsync(
            new VerifyUserCommand(user.Id, "123456"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.UserNotPendingVerification);
        user.Status.Should().Be(UserStatus.Active);
    }

    public static TheoryData<string, bool, int?> InvalidOtpCases()
    {
        return new()
        {
            { "   ", true, 5 },
            { "123456", false, 5 },
            { "123456", true, null },
            { "123456", true, -5 },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidOtpCases))]
    public async Task HandleAsyncInvalidOrExpiredOtpReturnsBadRequest(
        string otpArgument,
        bool hasStoredHash,
        int? expiresInMinutes
    )
    {
        var user = users.FindReturns(
            NewUser(
                statusId: KnownIds.UserStatusTypes.Pending,
                otpCodeHash: hasStoredHash ? FakeOneTimeCodeHasher.Prefix + "123456" : null,
                otpExpiresAt: expiresInMinutes is null
                    ? null
                    : clock.UtcNow.AddMinutes(expiresInMinutes.Value)
            )
        );

        var result = await sut.HandleAsync(
            new VerifyUserCommand(user.Id, otpArgument),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.OtpInvalidOrExpired);
    }

    [Fact]
    public async Task HandleAsyncWrongCodeReturnsBadRequestAndKeepsTheAccountPending()
    {
        var user = users.FindReturns(NewPendingWithOtp(clock, code: "the-real-code"));

        var result = await sut.HandleAsync(
            new VerifyUserCommand(user.Id, "a-wrong-code"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.OtpInvalidOrExpired);
        user.Status.Should().Be(UserStatus.Pending);
    }

    [Fact]
    public async Task HandleAsyncCorrectCodeActivatesUserAndClearsOtp()
    {
        var user = users.FindReturns(NewPendingWithOtp(clock, code: "the-real-code"));

        var result = await sut.HandleAsync(
            new VerifyUserCommand(user.Id, "  the-real-code  "),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
        user.VerificationCode.Hash.Should().BeNull();
        user.VerificationCode.ExpiresAt.Should().BeNull();
    }
}
