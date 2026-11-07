using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Accounts;

public sealed class AccountLockoutLogTests
{
    private static readonly IPAddress Address = IPAddress.Parse("203.0.113.7");

    private readonly RecordingLogger<AccountLockoutLog> logger = new();
    private readonly RecordingAuditTrail trail = new();
    private readonly TestCurrentUser currentUser = TestCurrentUser.Anonymous();
    private readonly AccountLockoutLog sut;

    public AccountLockoutLogTests()
    {
        var origin = Substitute.For<IRequestOrigin>();
        origin.ClientAddress.Returns(Address);
        sut = new AccountLockoutLog(
            new PasswordLockoutOptions { MaxFailedAttempts = 3 },
            new TwoFactorOptions { MaxFailedAttempts = 4 },
            logger,
            new AuditWriter(trail, currentUser, origin, new RecordingLogger<AuditWriter>())
        );
    }

    private static AuditRecord ExpectedEntry(AuditAction action, UserId account)
    {
        return new AuditRecord(
            action,
            AuditActorType.Anonymous,
            ActorId: null,
            Address,
            new AuditEntity(AuditEntityType.User, account.Value),
            [],
            AuditOutcome.Succeeded,
            ErrorCode: null
        );
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedRecordsTheLockoutWithoutTheAccount()
    {
        var userId = UserId.New();

        await sut.HandleAsync(new PasswordLocked(userId), TestContext.Current.CancellationToken);

        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry
            .Message.Should()
            .Be("An account was locked after 3 wrong passwords")
            .And.NotContain(userId.ToString());
        trail
            .Records.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(ExpectedEntry(AuditAction.PasswordLocked, userId));
    }

    [Fact]
    public async Task HandleAsyncTwoFactorLockedRecordsTheLockoutWithoutTheAccount()
    {
        var userId = UserId.New();

        await sut.HandleAsync(new TwoFactorLocked(userId), TestContext.Current.CancellationToken);

        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry
            .Message.Should()
            .Be("The second factor of an account was locked after 4 wrong codes")
            .And.NotContain(userId.ToString());
        trail
            .Records.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(ExpectedEntry(AuditAction.TwoFactorLocked, userId));
    }
}
