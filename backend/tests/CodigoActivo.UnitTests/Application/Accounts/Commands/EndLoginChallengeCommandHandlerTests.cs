using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
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
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly EndLoginChallengeCommandHandler sut;

    public EndLoginChallengeCommandHandlerTests()
    {
        sut = new EndLoginChallengeCommandHandler(users, uow);
    }

    [Fact]
    public async Task HandleAsyncUserMissingSucceedsWithoutSaving()
    {
        users.Finds(null);

        var result = await sut.HandleAsync(
            new EndLoginChallengeCommand(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAnotherChallengeKeepsTheOpenOneWithoutSaving()
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
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncOpenChallengeClosesItAndSaves()
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
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
