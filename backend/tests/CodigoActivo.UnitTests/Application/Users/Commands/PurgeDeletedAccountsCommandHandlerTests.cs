using AwesomeAssertions;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class PurgeDeletedAccountsCommandHandlerTests
{
    private readonly IDeletedAccountRepository deletedAccounts =
        Substitute.For<IDeletedAccountRepository>();
    private readonly TestClock clock = new();
    private readonly PurgeDeletedAccountsCommandHandler sut;

    public PurgeDeletedAccountsCommandHandlerTests()
    {
        sut = new PurgeDeletedAccountsCommandHandler(deletedAccounts, clock);
    }

    [Fact]
    public async Task HandleAsyncRemovesCopiesDeletedUpToThePurgeCutoffAndReportsHowMany()
    {
        deletedAccounts
            .RemoveDeletedUpToAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(5);

        var purged = await sut.HandleAsync(
            new PurgeDeletedAccountsCommand(),
            TestContext.Current.CancellationToken
        );

        purged.Should().Be(5);
        await deletedAccounts
            .Received(1)
            .RemoveDeletedUpToAsync(
                DeletedAccount.PurgeCutoff(clock.UtcNow),
                TestContext.Current.CancellationToken
            );
    }
}
