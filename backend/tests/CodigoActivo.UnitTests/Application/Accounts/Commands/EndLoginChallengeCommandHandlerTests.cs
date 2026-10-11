using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class EndLoginChallengeCommandHandlerTests
{
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
            new EndLoginChallengeCommand(UserId.New(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncAnotherChallengeKeepsTheOpenOne()
    {
        var openChallenge = Guid.NewGuid();
        var user = users.FindReturns(NewUser());
        user.StartLoginChallenge(openChallenge);

        var result = await sut.HandleAsync(
            new EndLoginChallengeCommand(user.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.LoginChallengeId.Should().Be(openChallenge);
    }

    [Fact]
    public async Task HandleAsyncOpenChallengeClosesIt()
    {
        var openChallenge = Guid.NewGuid();
        var user = users.FindReturns(NewUser());
        user.StartLoginChallenge(openChallenge);

        var result = await sut.HandleAsync(
            new EndLoginChallengeCommand(user.Id, openChallenge),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.LoginChallengeId.Should().BeNull();
    }
}
