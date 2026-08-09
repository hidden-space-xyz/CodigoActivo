using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Commands;

public sealed class VerifyUserCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly VerifyUserCommandHandler sut;

    public VerifyUserCommandHandlerTests()
    {
        sut = new VerifyUserCommandHandler(
            users,
            uow,
            clock,
            new OtpValidator(clock, new FakePasswordHasher())
        );
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

        var result = await sut.HandleAsync(
            new VerifyUserCommand(Guid.NewGuid(), "123456"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncUserNotPendingReturnsBadRequest()
    {
        var user = users.FindReturns(
            NewUser(
                statusId: SeedIds.UserStatusTypes.Active,
                otpCodeHash: FakePasswordHasher.Prefix + "123456",
                otpExpiresAt: clock.UtcNow.AddMinutes(5)
            )
        );

        var result = await sut.HandleAsync(
            new VerifyUserCommand(user.Id, "123456"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.OtpInvalidOrExpired);
        await AssertNotSavedAsync();
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
                statusId: SeedIds.UserStatusTypes.Pending,
                otpCodeHash: hasStoredHash ? FakePasswordHasher.Prefix + "123456" : null,
                otpExpiresAt: expiresInMinutes is null
                    ? null
                    : clock.UtcNow.AddMinutes(expiresInMinutes.Value)
            )
        );

        var result = await sut.HandleAsync(
            new VerifyUserCommand(user.Id, otpArgument),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.OtpInvalidOrExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWrongCodeReturnsBadRequestWithoutPersisting()
    {
        var user = users.FindReturns(NewPendingWithOtp(clock, code: "the-real-code"));

        var result = await sut.HandleAsync(
            new VerifyUserCommand(user.Id, "a-wrong-code"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.OtpInvalidOrExpired);
        user.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Pending);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncCorrectCodeActivatesUserAndClearsOtp()
    {
        var user = users.FindReturns(NewPendingWithOtp(clock, code: "the-real-code"));
        users
            .GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(NewUser(id: user.Id));

        var result = await sut.HandleAsync(
            new VerifyUserCommand(user.Id, "  the-real-code  "),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        user.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        user.OtpCodeHash.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
