using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class RemoveExpiredSessionsCommandHandlerTests
{
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly TestClock clock = new();
    private readonly RemoveExpiredSessionsCommandHandler sut;

    public RemoveExpiredSessionsCommandHandlerTests()
    {
        sut = new RemoveExpiredSessionsCommandHandler(sessions, clock);
    }

    [Fact]
    public async Task HandleAsyncRemovesEveryUsersSessionsExpiredByNowAndReportsHowMany()
    {
        sessions
            .RemoveExpiredAsync(
                Arg.Any<DateTimeOffset>(),
                Arg.Any<UserId?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(3);

        var removed = await sut.HandleAsync(
            new RemoveExpiredSessionsCommand(),
            TestContext.Current.CancellationToken
        );

        removed.Should().Be(3);
        await sessions
            .Received(1)
            .RemoveExpiredAsync(clock.UtcNow, null, TestContext.Current.CancellationToken);
    }
}
