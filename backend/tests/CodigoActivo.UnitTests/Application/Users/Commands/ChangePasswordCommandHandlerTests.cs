using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class ChangePasswordCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ChangePasswordCommandHandler sut;

    public ChangePasswordCommandHandlerTests()
    {
        sut = new ChangePasswordCommandHandler(users, hasher, clock, uow);
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);
        var request = new ChangePasswordRequest("old", "newpassword");

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncPasswordNotSetReturnsBadRequest()
    {
        var user = NewUser();
        user.PasswordHash = null;
        users.FindReturns(user);
        var request = new ChangePasswordRequest("old", "newpassword");

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserPasswordNotSet);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncIncorrectCurrentPasswordReturnsBadRequest()
    {
        var user = NewUser();
        user.PasswordHash = hasher.Hash("correct");
        users.FindReturns(user);
        var request = new ChangePasswordRequest("wrong", "newpassword");

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidCurrentPasswordRehashesAndPersists()
    {
        var user = NewUser();
        user.PasswordHash = hasher.Hash("correct");
        users.FindReturns(user);
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new ChangePasswordRequest("correct", "brandnew");

        var result = await sut.HandleAsync(
            new ChangePasswordCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(hasher.Hash("brandnew"));
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
