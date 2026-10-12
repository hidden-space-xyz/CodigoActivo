using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Accounts.Sessions;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Queries;

public sealed class GetLoginChallengeTicketQueryHandlerTests
{
    private static readonly Guid ChallengeId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset LockedAt = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeReadStore store = new();
    private readonly TestClock clock = new();
    private readonly TwoFactorOptions options = new();
    private readonly GetLoginChallengeTicketQueryHandler sut;

    public GetLoginChallengeTicketQueryHandlerTests()
    {
        sut = new GetLoginChallengeTicketQueryHandler(
            store,
            new FakeQueryExecutor(),
            clock,
            options
        );
    }

    private DateTimeOffset OpenedAMinuteAgo => clock.UtcNow.AddMinutes(-1);

    private Task<LoginChallengeTicket?> QueryAsync(Guid userId, Guid challengeId)
    {
        return sut.HandleAsync(
            new GetLoginChallengeTicketQuery(
                new LoginChallengeKey(UserId.From(userId), LoginChallengeId.From(challengeId))
            ),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncOpenChallengeReturnsTicketWithCredentialStamp()
    {
        var user = NewUserRow(
            passwordHash: "fake:secret",
            loginChallengeId: ChallengeId,
            loginChallengeStartedAt: OpenedAMinuteAgo
        );
        store.Users.AddRange([
            NewUserRow(loginChallengeId: Guid.NewGuid(), loginChallengeStartedAt: OpenedAMinuteAgo),
            user,
        ]);

        var ticket = await QueryAsync(user.Id, ChallengeId);

        ticket.Should().Be(new LoginChallengeTicket(CredentialStamps.For("fake:secret")));
        ticket!.Accepts(CredentialStamps.For("fake:secret")).Should().BeTrue();
        ticket.Accepts(CredentialStamps.For("fake:other")).Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncAnotherChallengeReturnsNull()
    {
        var user = NewUserRow(
            loginChallengeId: ChallengeId,
            loginChallengeStartedAt: OpenedAMinuteAgo
        );
        store.Users.Add(user);

        var ticket = await QueryAsync(user.Id, Guid.NewGuid());

        ticket.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncChallengeOlderThanItsLifetimeReturnsNull()
    {
        var user = NewUserRow(
            loginChallengeId: ChallengeId,
            loginChallengeStartedAt: clock.UtcNow - options.ChallengeLifetime
        );
        store.Users.Add(user);

        var ticket = await QueryAsync(user.Id, ChallengeId);

        ticket.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNull()
    {
        store.Users.Add(
            NewUserRow(loginChallengeId: ChallengeId, loginChallengeStartedAt: OpenedAMinuteAgo)
        );

        var ticket = await QueryAsync(Guid.NewGuid(), ChallengeId);

        ticket.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAccountNotActiveReturnsNull()
    {
        var user = NewUserRow(
            statusId: KnownIds.UserStatusTypes.Pending,
            loginChallengeId: ChallengeId,
            loginChallengeStartedAt: OpenedAMinuteAgo
        );
        store.Users.Add(user);

        var ticket = await QueryAsync(user.Id, ChallengeId);

        ticket.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAccountWithoutPasswordReturnsNull()
    {
        var user = NewUserRow(
            passwordHash: null,
            loginChallengeId: ChallengeId,
            loginChallengeStartedAt: OpenedAMinuteAgo
        );
        store.Users.Add(user);

        var ticket = await QueryAsync(user.Id, ChallengeId);

        ticket.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedReturnsNull()
    {
        var user = NewUserRow(
            loginChallengeId: ChallengeId,
            loginChallengeStartedAt: OpenedAMinuteAgo,
            passwordLockedAt: LockedAt
        );
        store.Users.Add(user);

        var ticket = await QueryAsync(user.Id, ChallengeId);

        ticket.Should().BeNull();
    }
}
