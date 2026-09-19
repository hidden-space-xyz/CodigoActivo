using System.Linq.Expressions;
using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Security;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class SessionTicketValidatorTests
{
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly RecordingLogger<SessionTicketValidator> logger = new();

    private SessionTicketValidator Build()
    {
        return new SessionTicketValidator(
            null!,
            sessions,
            Substitute.For<IUnitOfWork>(),
            new TestClock(),
            new SessionLifetimeOptions(),
            logger
        );
    }

    private void RemovedRows(int rows)
    {
        sessions
            .RemoveAsync(
                Arg.Any<Expression<Func<UserSession, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(rows);
    }

    private static ClaimsPrincipal Ticket(Guid userId, string? sessionId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (sessionId is not null)
        {
            claims.Add(new Claim("sid", sessionId));
        }

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
        );
    }

    [Fact]
    public async Task EndSessionAsyncRevokedRowLogsTheUserIdOnly()
    {
        RemovedRows(1);
        var userId = Guid.NewGuid();

        await Build()
            .EndSessionAsync(
                Ticket(userId, Guid.NewGuid().ToString()),
                TestContext.Current.CancellationToken
            );

        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Be($"Session ended for user {userId}");
    }

    [Fact]
    public async Task EndSessionAsyncAlreadyGoneRowLogsNothing()
    {
        RemovedRows(0);

        await Build()
            .EndSessionAsync(
                Ticket(Guid.NewGuid(), Guid.NewGuid().ToString()),
                TestContext.Current.CancellationToken
            );

        logger.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task EndSessionAsyncTicketWithoutUsableSessionIdTouchesNothing(string? sessionId)
    {
        RemovedRows(1);

        await Build()
            .EndSessionAsync(
                Ticket(Guid.NewGuid(), sessionId),
                TestContext.Current.CancellationToken
            );

        await sessions
            .DidNotReceiveWithAnyArgs()
            .RemoveAsync(
                Arg.Any<Expression<Func<UserSession, bool>>>(),
                TestContext.Current.CancellationToken
            );
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task EndSessionAsyncWithoutAPrincipalTouchesNothing()
    {
        RemovedRows(1);

        await Build().EndSessionAsync(null, TestContext.Current.CancellationToken);

        await sessions
            .DidNotReceiveWithAnyArgs()
            .RemoveAsync(
                Arg.Any<Expression<Func<UserSession, bool>>>(),
                TestContext.Current.CancellationToken
            );
        logger.Entries.Should().BeEmpty();
    }
}
