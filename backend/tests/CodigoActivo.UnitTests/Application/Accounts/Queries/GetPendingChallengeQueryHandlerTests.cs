using AwesomeAssertions;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Queries;

public sealed class GetPendingChallengeQueryHandlerTests
{
    private static readonly Guid ChallengeId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset LockedAt = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeReadStore store = new();
    private readonly GetPendingChallengeQueryHandler sut;

    public GetPendingChallengeQueryHandlerTests()
    {
        sut = new GetPendingChallengeQueryHandler(store, new FakeQueryExecutor());
    }

    private Task<PendingChallenge?> QueryAsync(Guid userId)
    {
        return sut.HandleAsync(
            new GetPendingChallengeQuery(UserId.From(userId)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncOpenChallengeReturnsChallengeWithCredentialStamp()
    {
        var user = NewUserRow(passwordHash: "fake:secret", loginChallengeId: ChallengeId);
        store.Users.AddRange([NewUserRow(loginChallengeId: Guid.NewGuid()), user]);

        var pending = await QueryAsync(user.Id);

        pending.Should().Be(new PendingChallenge(ChallengeId, CredentialStamps.For("fake:secret")));
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNull()
    {
        store.Users.Add(NewUserRow(loginChallengeId: ChallengeId));

        var pending = await QueryAsync(Guid.NewGuid());

        pending.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncWithoutOpenChallengeReturnsNull()
    {
        var user = NewUserRow(loginChallengeId: null);
        store.Users.Add(user);

        var pending = await QueryAsync(user.Id);

        pending.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAccountNotActiveReturnsNull()
    {
        var user = NewUserRow(
            statusId: KnownIds.UserStatusTypes.Pending,
            loginChallengeId: ChallengeId
        );
        store.Users.Add(user);

        var pending = await QueryAsync(user.Id);

        pending.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAccountWithoutPasswordReturnsNull()
    {
        var user = NewUserRow(passwordHash: null, loginChallengeId: ChallengeId);
        store.Users.Add(user);

        var pending = await QueryAsync(user.Id);

        pending.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedReturnsNull()
    {
        var user = NewUserRow(loginChallengeId: ChallengeId, passwordLockedAt: LockedAt);
        store.Users.Add(user);

        var pending = await QueryAsync(user.Id);

        pending.Should().BeNull();
    }
}
