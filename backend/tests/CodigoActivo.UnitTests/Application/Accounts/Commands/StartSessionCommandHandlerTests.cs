using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class StartSessionCommandHandlerTests
{
    private const string PasswordHash = FakePasswordHasher.Prefix + "secret";

    public static TheoryData<Guid, string?> AccountsThatCannotSignIn =>
        new()
        {
            { SeedIds.UserStatusTypes.Pending, PasswordHash },
            { SeedIds.UserStatusTypes.Blocked, PasswordHash },
            { SeedIds.UserStatusTypes.Dependent, PasswordHash },
            { SeedIds.UserStatusTypes.Active, null },
        };

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly SessionLifetimeOptions options = new() { Lifetime = TimeSpan.FromHours(8) };
    private readonly StartSessionCommandHandler sut;

    public StartSessionCommandHandlerTests()
    {
        sut = new StartSessionCommandHandler(users, sessions, uow, clock, options);
    }

    private Task<Result<SessionTicket>> StartAsync(Guid userId)
    {
        return sut.HandleAsync(
            new StartSessionCommand(userId),
            TestContext.Current.CancellationToken
        );
    }

    private async Task AssertNoSessionStartedAsync()
    {
        await sessions
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        await sessions
            .DidNotReceiveWithAnyArgs()
            .RemoveExpiredAsync(default, default, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsInvalidCredentials()
    {
        users.Finds(null);

        var result = await StartAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        await AssertNoSessionStartedAsync();
    }

    [Theory]
    [MemberData(nameof(AccountsThatCannotSignIn))]
    public async Task HandleAsyncAccountThatCannotSignInReturnsInvalidCredentials(
        Guid statusId,
        string? passwordHash
    )
    {
        var user = NewUser(statusId: statusId, passwordHash: passwordHash);
        users.Finds(user);

        var result = await StartAsync(user.Id);

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        await AssertNoSessionStartedAsync();
    }

    [Fact]
    public async Task HandleAsyncActiveAccountStoresASessionAndReturnsItsTicket()
    {
        var user = NewUser(
            email: "ana@test.com",
            isAdmin: true,
            statusId: SeedIds.UserStatusTypes.Active,
            passwordHash: PasswordHash
        );
        users.Finds(user);
        var added = new List<UserSession>();
        await sessions.AddAsync(Arg.Do<UserSession>(added.Add), Arg.Any<CancellationToken>());

        var result = await StartAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        var session = added.Should().ContainSingle().Subject;
        session.UserId.Should().Be(user.Id);
        session.CreatedAt.Should().Be(clock.UtcNow);
        session.ExpiresAt.Should().Be(clock.UtcNow.AddHours(8));
        result
            .Value.Should()
            .Be(
                new SessionTicket(
                    session.Id,
                    new SessionIdentity(
                        user.Id,
                        user.FirstName,
                        user.LastName,
                        "ana@test.com",
                        true,
                        CredentialStamps.For(PasswordHash)
                    )
                )
            );
        Received.InOrder(() =>
        {
            _ = sessions.RemoveExpiredAsync(clock.UtcNow, user.Id, Arg.Any<CancellationToken>());
            _ = sessions.AddAsync(session, Arg.Any<CancellationToken>());
            _ = uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }
}
