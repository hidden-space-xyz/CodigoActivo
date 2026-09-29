using System.Security.Claims;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.Authentication.Cookies;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class SessionTicketValidatorTests
{
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();

    private SessionTicketValidator Build()
    {
        return new SessionTicketValidator(
            new StartSessionCommandHandler(
                Substitute.For<IUserRepository>(),
                sessions,
                Substitute.For<IUnitOfWork>(),
                new TestClock(),
                new SessionLifetimeOptions()
            ),
            new EndSessionCommandHandler(sessions),
            new GetSessionIdentityQueryHandler(
                new FakeReadStore(),
                new FakeQueryExecutor(),
                new TestClock()
            )
        );
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
    public async Task EndSessionAsyncRevokesTheRowNamedByTheTicket()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await Build()
            .EndSessionAsync(
                Ticket(userId, sessionId.ToString()),
                TestContext.Current.CancellationToken
            );

        await sessions.Received(1).EndAsync(sessionId, userId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task EndSessionAsyncTicketWithoutUsableSessionIdTouchesNothing(string? sessionId)
    {
        await Build()
            .EndSessionAsync(
                Ticket(Guid.NewGuid(), sessionId),
                TestContext.Current.CancellationToken
            );

        await sessions
            .DidNotReceiveWithAnyArgs()
            .EndAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EndSessionAsyncWithoutAPrincipalTouchesNothing()
    {
        await Build().EndSessionAsync(null, TestContext.Current.CancellationToken);

        await sessions
            .DidNotReceiveWithAnyArgs()
            .EndAsync(default, default, TestContext.Current.CancellationToken);
    }
}
