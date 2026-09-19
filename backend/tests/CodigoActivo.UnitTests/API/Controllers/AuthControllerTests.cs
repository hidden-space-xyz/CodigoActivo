using System.Linq.Expressions;
using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Controllers;
using CodigoActivo.API.Security;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            .RemoveAsync(
                Arg.Any<Expression<Func<UserSession, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns<Task<int>>(_ => throw new InvalidOperationException("db down"));
        var validator = new SessionTicketValidator(
            null!,
            sessions,
            Substitute.For<IUnitOfWork>(),
            new TestClock(),
            new SessionLifetimeOptions(),
            NullLogger<SessionTicketValidator>.Instance
        );

        var authenticationService = Substitute.For<IAuthenticationService>();
        var services = new ServiceCollection();
        services.AddSingleton(authenticationService);
        services.AddSingleton(validator);
        services.AddSingleton<ILogger<AuthController>>(NullLogger<AuthController>.Instance);
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

        var result = await controller.LogoutAsync(TestContext.Current.CancellationToken);

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
