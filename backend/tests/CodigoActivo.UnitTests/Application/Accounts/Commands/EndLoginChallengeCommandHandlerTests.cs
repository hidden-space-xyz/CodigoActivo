using AwesomeAssertions;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class EndLoginChallengeCommandHandlerTests
{
    private static readonly DateTimeOffset OpenedAt = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly EndLoginChallengeCommandHandler sut;

    public EndLoginChallengeCommandHandlerTests()
    {
        sut = new EndLoginChallengeCommandHandler(users);
    }

    [Fact]
    public async Task HandleAsyncUserMissingSucceeds()
    {
        users.Finds(null);

        var result = await sut.HandleAsync(
            new EndLoginChallengeCommand(
                new LoginChallengeKey(UserId.New(), LoginChallengeId.New())
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncAnotherChallengeKeepsTheOpenOne()
    {
        var openChallenge = LoginChallengeId.New();
        var user = users.FindReturns(NewUser());
        user.StartLoginChallenge(openChallenge, OpenedAt);

        var result = await sut.HandleAsync(
            new EndLoginChallengeCommand(new LoginChallengeKey(user.Id, LoginChallengeId.New())),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.LoginChallengeId.Should().Be(openChallenge);
    }

    [Fact]
    public async Task HandleAsyncOpenChallengeClosesIt()
    {
        var openChallenge = LoginChallengeId.New();
        var user = users.FindReturns(NewUser());
        user.StartLoginChallenge(openChallenge, OpenedAt);

        var result = await sut.HandleAsync(
            new EndLoginChallengeCommand(new LoginChallengeKey(user.Id, openChallenge)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.LoginChallengeId.Should().BeNull();
    }
}
