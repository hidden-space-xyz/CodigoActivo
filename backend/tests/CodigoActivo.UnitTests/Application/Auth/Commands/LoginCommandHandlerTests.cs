using AwesomeAssertions;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Commands;

public sealed class LoginCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly AccountVerificationOptions verification = new();
    private readonly LoginCommandHandler sut;

    public LoginCommandHandlerTests()
    {
        sut = new LoginCommandHandler(users, uow, clock, new FakePasswordHasher(), verification);
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUserNotFoundReturnsUnauthorized()
    {
        User? missing = null;
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(missing);

        var result = await sut.HandleAsync(
            new LoginCommand(new LoginRequest("nobody@test.com", "password123")),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task HandleAsyncPasswordHashNotSetReturnsUnauthorized(string? hash)
    {
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(NewUser(passwordHash: hash));

        var result = await sut.HandleAsync(
            new LoginCommand(new LoginRequest("ana@test.com", "password123")),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncPasswordDoesNotVerifyReturnsUnauthorized()
    {
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(NewUser(passwordHash: "fake:correct"));

        var result = await sut.HandleAsync(
            new LoginCommand(new LoginRequest("ana@test.com", "wrong")),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        await AssertNotSavedAsync();
    }

    [Theory]
    [MemberData(nameof(BlockedStatuses))]
    public async Task HandleAsyncNonActiveStatusReturnsForbidden(Guid statusId, ErrorCode expected)
    {
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(NewUser(statusId: statusId));

        var result = await sut.HandleAsync(
            new LoginCommand(new LoginRequest("ana@test.com", "password123")),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Forbidden, expected);
        await AssertNotSavedAsync();
    }

    public static TheoryData<Guid, ErrorCode> BlockedStatuses()
    {
        return new()
        {
            { SeedIds.UserStatusTypes.Blocked, ErrorCode.UserAccountBlocked },
            { SeedIds.UserStatusTypes.Dependent, ErrorCode.UserAccountIsDependent },
            { SeedIds.UserStatusTypes.Pending, ErrorCode.UserAccountPendingVerification },
        };
    }

    [Fact]
    public async Task HandleAsyncPendingUserVerificationNotRequiredActivatesUser()
    {
        verification.Required = false;
        var user = NewUser(statusId: SeedIds.UserStatusTypes.Pending, otpCodeHash: "ABCDEF");
        users.GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        users
            .GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(NewUser(id: user.Id, statusId: SeedIds.UserStatusTypes.Active));

        var result = await sut.HandleAsync(
            new LoginCommand(new LoginRequest("ana@test.com", "password123")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        user.OtpCodeHash.Should().BeNull();
        result.Value.Status.Id.Should().Be(SeedIds.UserStatusTypes.Active);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncValidCredentialsTrimsIdentifierAndRecordsLogin()
    {
        var user = NewUser();
        users.GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        var result = await sut.HandleAsync(
            new LoginCommand(new LoginRequest("  ana@test.com  ", "password123")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Email.Should().Be("ana@test.com");
        result.Value.Type.Should().BeNull();
        user.LastLoginAt.Should().NotBeNull();
        await users
            .Received(1)
            .GetByEmailOrPhoneAsync("ana@test.com", Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
