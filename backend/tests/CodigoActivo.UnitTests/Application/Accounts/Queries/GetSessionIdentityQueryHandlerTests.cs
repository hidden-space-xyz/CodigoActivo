using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Queries;

public sealed class GetSessionIdentityQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = new("dddddddd-0000-0000-0000-000000000001");

    private readonly FakeReadStore store = new();
    private readonly GetSessionIdentityQueryHandler sut;

    public GetSessionIdentityQueryHandlerTests()
    {
        sut = new GetSessionIdentityQueryHandler(
            store,
            new FakeQueryExecutor(),
            new TestClock(Now)
        );
    }

    private static UserSessionRow Session(Guid userId, DateTimeOffset expiresAt, Guid? id = null)
    {
        return new()
        {
            Id = id ?? SessionId,
            UserId = userId,
            CreatedAt = Now.AddHours(-1),
            ExpiresAt = expiresAt,
        };
    }

    private Task<SessionIdentity?> QueryAsync(Guid userId, Guid? sessionId = null)
    {
        return sut.HandleAsync(
            new GetSessionIdentityQuery(userId, sessionId ?? SessionId),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncLiveSessionReturnsIdentityWithCredentialStamp()
    {
        var user = NewUserRow(email: "ana@test.com", passwordHash: "fake:secret", isAdmin: true);
        store.Users.AddRange([NewUserRow(), user]);
        store.UserSessions.Add(Session(user.Id, Now.AddMinutes(1)));

        var identity = await QueryAsync(user.Id);

        identity
            .Should()
            .Be(
                new SessionIdentity(
                    user.Id,
                    "Ana",
                    "Ruiz",
                    "ana@test.com",
                    true,
                    CredentialStamps.For("fake:secret")
                )
            );
    }

    [Fact]
    public async Task HandleAsyncSessionExpiringNowReturnsNull()
    {
        var user = NewUserRow();
        store.Users.Add(user);
        store.UserSessions.Add(Session(user.Id, Now));

        var identity = await QueryAsync(user.Id);

        identity.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncUnknownSessionReturnsNull()
    {
        var user = NewUserRow();
        store.Users.Add(user);
        store.UserSessions.Add(Session(user.Id, Now.AddDays(1), Guid.NewGuid()));

        var identity = await QueryAsync(user.Id);

        identity.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncSessionOfAnotherAccountReturnsNull()
    {
        var user = NewUserRow();
        var other = NewUserRow();
        store.Users.AddRange([user, other]);
        store.UserSessions.Add(Session(other.Id, Now.AddDays(1)));

        var identity = await QueryAsync(user.Id);

        identity.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNull()
    {
        var missingUserId = Guid.NewGuid();
        store.Users.Add(NewUserRow());
        store.UserSessions.Add(Session(missingUserId, Now.AddDays(1)));

        var identity = await QueryAsync(missingUserId);

        identity.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAccountNotActiveReturnsNull()
    {
        var user = NewUserRow(statusId: SeedIds.UserStatusTypes.Pending);
        store.Users.Add(user);
        store.UserSessions.Add(Session(user.Id, Now.AddDays(1)));

        var identity = await QueryAsync(user.Id);

        identity.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAccountLockedAfterWrongPasswordsReturnsNull()
    {
        var user = NewUserRow(passwordLockedAt: Now.AddMinutes(-1));
        store.Users.Add(user);
        store.UserSessions.Add(Session(user.Id, Now.AddDays(1)));

        var identity = await QueryAsync(user.Id);

        identity.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAccountWithoutPasswordReturnsNull()
    {
        var user = NewUserRow(passwordHash: null);
        store.Users.Add(user);
        store.UserSessions.Add(Session(user.Id, Now.AddDays(1)));

        var identity = await QueryAsync(user.Id);

        identity.Should().BeNull();
    }
}
