using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Accounts;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.API.Contracts;
using CodigoActivo.API.Errors;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
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
    private static EndLoginChallengeCommandHandler EndLoginChallengeHandler()
    {
        return new EndLoginChallengeCommandHandler(Substitute.For<IUserRepository>());
    }

    [Fact]
    public async Task LogoutAsyncSessionRowDeletionThrowsStillClearsBothCookiesAndReturnsNoContent()
    {
        var sessions = Substitute.For<IUserSessionRepository>();
        sessions
            .EndAsync(Arg.Any<UserSessionId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("db down"));
        var validator = new SessionTicketValidator(
            new EndSessionCommandHandler(sessions),
            new GetSessionIdentityQueryHandler(
                new FakeReadStore(),
                new FakeQueryExecutor(),
                new TestClock()
            )
        );

        var challengeTickets = new TwoFactorTicketValidator(
            new GetLoginChallengeTicketQueryHandler(
                new FakeReadStore(),
                new FakeQueryExecutor(),
                new TestClock(),
                new TwoFactorOptions()
            ),
            EndLoginChallengeHandler()
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

    [Fact]
    public async Task LoginAsyncChallengeClosedBeforeItsTicketAnswersInvalidCredentials()
    {
        var challenge = new LoginChallengeKey(UserId.New(), LoginChallengeId.New());
        var login = Substitute.For<ICommandHandler<LoginCommand, Result<LoginChallengeKey>>>();
        login.HandleAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>()).Returns(challenge);
        var tickets = new TwoFactorTicketValidator(
            Substitute.For<IQueryHandler<GetLoginChallengeTicketQuery, LoginChallengeTicket?>>(),
            Substitute.For<ICommandHandler<EndLoginChallengeCommand, Result>>()
        );
        var (controller, authentication) = Controller();

        var result = await controller.LoginAsync(
            new LoginRequest("ada@example.org", "secret"),
            login,
            tickets,
            Substitute.For<IQueryHandler<GetLoginChallengeQuery, Result<LoginChallengeResponse>>>(),
            TestContext.Current.CancellationToken
        );

        ShouldBeInvalidCredentials(result.Result);
        await authentication
            .DidNotReceiveWithAnyArgs()
            .SignInAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task VerifyTwoFactorAsyncSessionGoneBeforeItsTicketAnswersInvalidCredentials()
    {
        var (controller, authentication) = Controller();
        var pending = await PendingLoginAsync();
        authentication
            .AuthenticateAsync(controller.HttpContext, TwoFactorAuthentication.Scheme)
            .Returns(
                AuthenticateResult.Success(
                    new AuthenticationTicket(pending, TwoFactorAuthentication.Scheme)
                )
            );
        var complete = Substitute.For<
            ICommandHandler<CompleteLoginCommand, Result<UserSessionId>>
        >();
        complete
            .HandleAsync(Arg.Any<CompleteLoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(UserSessionId.New());

        var result = await controller.VerifyTwoFactorAsync(
            new TwoFactorLoginRequest("123456"),
            complete,
            Substitute.For<IQueryHandler<GetAccountQuery, Result<UserResponse>>>(),
            SessionTickets(Substitute.For<IUserSessionRepository>()),
            TestContext.Current.CancellationToken
        );

        ShouldBeInvalidCredentials(result.Result);
        await authentication
            .DidNotReceiveWithAnyArgs()
            .SignInAsync(default!, default, default!, default);
    }

    private static async Task<ClaimsPrincipal> PendingLoginAsync()
    {
        var challenge = new LoginChallengeKey(UserId.New(), LoginChallengeId.New());
        var tickets = Substitute.For<
            IQueryHandler<GetLoginChallengeTicketQuery, LoginChallengeTicket?>
        >();
        tickets
            .HandleAsync(Arg.Any<GetLoginChallengeTicketQuery>(), Arg.Any<CancellationToken>())
            .Returns(new LoginChallengeTicket("stamp"));
        var principal = await new TwoFactorTicketValidator(
            tickets,
            Substitute.For<ICommandHandler<EndLoginChallengeCommand, Result>>()
        ).CreatePrincipalAsync(challenge, false, TestContext.Current.CancellationToken);
        return principal!;
    }

    private static SessionTicketValidator SessionTickets(IUserSessionRepository sessions)
    {
        return new SessionTicketValidator(
            new EndSessionCommandHandler(sessions),
            new GetSessionIdentityQueryHandler(
                new FakeReadStore(),
                new FakeQueryExecutor(),
                new TestClock()
            )
        );
    }

    private static (AuthController Controller, IAuthenticationService Authentication) Controller()
    {
        var authentication = Substitute.For<IAuthenticationService>();
        var services = new ServiceCollection().AddSingleton(authentication).BuildServiceProvider();
        var controller = new AuthController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services },
            },
        };
        return (controller, authentication);
    }

    private static void ShouldBeInvalidCredentials(ActionResult? result)
    {
        var problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        problem
            .Value.Should()
            .BeOfType<ApiErrorResponse>()
            .Which.Code.Should()
            .Be(ErrorCode.InvalidCredentials);
    }
}
