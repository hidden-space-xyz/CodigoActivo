using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Accounts;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.API.Controllers;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task LogoutAsyncSessionRowDeletionThrowsStillClearsBothCookiesAndReturnsNoContent()
    {
        var sessions = Substitute.For<IUserSessionRepository>();
        sessions
            .EndAsync(Arg.Any<UserSessionId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("db down"));
        var validator = new SessionTicketValidator(
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

        var challengeTickets = new TwoFactorTicketValidator(
            new GetPendingChallengeQueryHandler(new FakeReadStore(), new FakeQueryExecutor()),
            new EndLoginChallengeCommandHandler(
                Substitute.For<IUserRepository>(),
                Substitute.For<IUnitOfWork>()
            )
        );
        var authenticationService = Substitute.For<IAuthenticationService>();
        var services = new ServiceCollection();
        services.AddSingleton(authenticationService);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim("sid", Guid.NewGuid().ToString()),
                ],
                CookieAuthenticationDefaults.AuthenticationScheme
            )
        );

        var controller = new AuthController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };

        var result = await controller.LogoutAsync(
            validator,
            challengeTickets,
            NullLogger<AuthController>.Instance,
            TestContext.Current.CancellationToken
        );

        result.Should().BeOfType<NoContentResult>();
        await authenticationService
            .Received(1)
            .SignOutAsync(
                httpContext,
                CookieAuthenticationDefaults.AuthenticationScheme,
                Arg.Any<AuthenticationProperties?>()
            );
        await authenticationService
            .Received(1)
            .SignOutAsync(
                httpContext,
                TwoFactorAuthentication.Scheme,
                Arg.Any<AuthenticationProperties?>()
            );
    }
}
