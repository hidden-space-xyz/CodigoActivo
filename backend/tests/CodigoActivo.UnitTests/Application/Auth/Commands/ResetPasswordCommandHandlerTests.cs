using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Commands;

public sealed class ResetPasswordCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ResetPasswordCommandHandler sut;

    public ResetPasswordCommandHandlerTests()
    {
        sut = new ResetPasswordCommandHandler(
            users,
            uow,
            clock,
            new FakePasswordHasher(),
            new OtpValidator(clock, new FakePasswordHasher())
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_UserMissing_ReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await sut.HandleAsync(
            new ResetPasswordCommand(
                Guid.NewGuid(),
                new ResetPasswordRequest("some-code", "newPassword123")
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_NoCodeRequested_ReturnsBadRequest()
    {
        var user = users.FindReturns(NewUser());

        var result = await sut.HandleAsync(
            new ResetPasswordCommand(
                user.Id,
                new ResetPasswordRequest("some-code", "newPassword123")
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.PasswordResetInvalidOrExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_ExpiredCode_ReturnsBadRequest()
    {
        var user = users.FindReturns(
            NewUserWithResetCode(clock, expiresAt: clock.UtcNow.AddMinutes(-5))
        );

        var result = await sut.HandleAsync(
            new ResetPasswordCommand(
                user.Id,
                new ResetPasswordRequest("the-reset-code", "newPassword123")
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.PasswordResetInvalidOrExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_WrongCode_ReturnsBadRequestWithoutPersisting()
    {
        var user = users.FindReturns(NewUserWithResetCode(clock, code: "the-real-code"));
        var previousPasswordHash = user.PasswordHash;

        var result = await sut.HandleAsync(
            new ResetPasswordCommand(
                user.Id,
                new ResetPasswordRequest("a-wrong-code", "newPassword123")
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.PasswordResetInvalidOrExpired);
        user.PasswordHash.Should().Be(previousPasswordHash);
        user.PasswordResetCodeHash.Should().NotBeNull("a wrong guess must not consume the code");
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_UserBlockedAfterRequest_ReturnsBadRequest()
    {
        var user = users.FindReturns(NewUserWithResetCode(clock, code: "the-reset-code"));
        user.UserStatusTypeId = SeedIds.UserStatusTypes.Blocked;

        var result = await sut.HandleAsync(
            new ResetPasswordCommand(
                user.Id,
                new ResetPasswordRequest("the-reset-code", "newPassword123")
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.PasswordResetInvalidOrExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_CorrectCode_ChangesPasswordAndClearsCode()
    {
        var user = users.FindReturns(NewUserWithResetCode(clock, code: "the-reset-code"));

        var result = await sut.HandleAsync(
            new ResetPasswordCommand(
                user.Id,
                new ResetPasswordRequest("  THE-RESET-CODE  ", "newPassword123")
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(FakePasswordHasher.Prefix + "newPassword123");
        user.PasswordResetCodeHash.Should().BeNull();
        user.PasswordResetExpiresAt.Should().BeNull();
        user.PasswordResetLastSentAt.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
