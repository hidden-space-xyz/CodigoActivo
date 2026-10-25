using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class PurgeUnverifiedAccountsCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IAccountErasureStore erasureStore = Substitute.For<IAccountErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly PurgeUnverifiedAccountsCommandHandler sut;

    public PurgeUnverifiedAccountsCommandHandlerTests()
    {
        sut = new PurgeUnverifiedAccountsCommandHandler(
            users,
            AccountErasers.Create(users, erasureStore, uow),
            uow.RunsTransactions(),
            clock
        );
    }

    private User Unverified(int daysAgo)
    {
        var account = NewUser(statusId: KnownIds.UserStatusTypes.Pending);
        Persisted.Overwrite(account, new { CreatedAt = clock.UtcNow.AddDays(-daysAgo) });
        return account;
    }

    private void Lists(params User[] accounts)
    {
        users
            .ListUnverifiedRegisteredUpToAsync(
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(accounts);
    }

    private Task<CodigoActivo.Domain.Common.Result<int>> HandleAsync()
    {
        return sut.HandleAsync(
            new PurgeUnverifiedAccountsCommand(),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncErasesTheAccountsStillUnverifiedTwentyFourHoursAfterRegistering()
    {
        var first = Unverified(daysAgo: 45);
        var second = Unverified(daysAgo: 1);
        Lists(first, second);
        users.LocksEveryAccount();

        var result = await HandleAsync();

        result.Value.Should().Be(2);
        await users
            .Received(1)
            .ListUnverifiedRegisteredUpToAsync(
                clock.UtcNow.AddDays(-1),
                Arg.Any<CancellationToken>()
            );
        users.Received(1).Remove(first);
        users.Received(1).Remove(second);
        await erasureStore.Received(1).LoadHouseholdAsync(first.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncKeepsAnAccountVerifiedSinceItWasListed()
    {
        var account = Unverified(daysAgo: 40);
        Lists(account);
        users.ReloadsAs(account, new { Status = UserStatus.Active });

        var result = await HandleAsync();

        result.Value.Should().Be(0);
        users.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Fact]
    public async Task HandleAsyncSkipsAnAccountGoneOnceLocked()
    {
        var account = Unverified(daysAgo: 40);
        Lists(account);
        users.LockAsync(account, Arg.Any<CancellationToken>()).Returns(false);

        var result = await HandleAsync();

        result.Value.Should().Be(0);
        users.DidNotReceiveWithAnyArgs().Remove(default!);
    }
}
