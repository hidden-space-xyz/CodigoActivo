using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class TwoFactorTicketValidatorTests
{
    private const string Stamp = "stamp-of-the-current-password";

    private readonly IQueryHandler<GetLoginChallengeTicketQuery, LoginChallengeTicket?> tickets =
        Substitute.For<IQueryHandler<GetLoginChallengeTicketQuery, LoginChallengeTicket?>>();

    private readonly ICommandHandler<EndLoginChallengeCommand, Result> endChallenge =
        Substitute.For<ICommandHandler<EndLoginChallengeCommand, Result>>();

    private readonly IAuthenticationService authentication =
        Substitute.For<IAuthenticationService>();

    private readonly LoginChallengeKey challenge = new(
        UserId.From(Guid.NewGuid()),
        LoginChallengeId.From(Guid.NewGuid())
    );

    private TwoFactorTicketValidator Build()
    {
        return new TwoFactorTicketValidator(tickets, endChallenge);
    }

    private void ChallengeIsOpen(string stamp)
    {
        tickets
            .HandleAsync(new GetLoginChallengeTicketQuery(challenge), Arg.Any<CancellationToken>())
            .Returns(new LoginChallengeTicket(stamp));
    }

    private async Task<ClaimsPrincipal> IssuedTicketAsync(bool keepSignedIn = false)
    {
        ChallengeIsOpen(Stamp);
        var principal = await Build()
            .CreatePrincipalAsync(challenge, keepSignedIn, TestContext.Current.CancellationToken);
        return principal!;
    }

    private CookieValidatePrincipalContext Context(ClaimsPrincipal principal)
    {
        var services = new ServiceCollection().AddSingleton(authentication).BuildServiceProvider();
        return new CookieValidatePrincipalContext(
            new DefaultHttpContext { RequestServices = services },
            new AuthenticationScheme(
                TwoFactorAuthentication.Scheme,
                null,
                typeof(CookieAuthenticationHandler)
            ),
            new CookieAuthenticationOptions(),
            new AuthenticationTicket(principal, TwoFactorAuthentication.Scheme)
        );
    }

    private static ClaimsPrincipal Without(ClaimsPrincipal principal, string claimType)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(
                principal.Claims.Where(claim => claim.Type != claimType),
                TwoFactorAuthentication.Scheme
            )
        );
    }

    private async Task ShouldBeRejectedAsync(CookieValidatePrincipalContext context)
    {
        context.Principal.Should().BeNull();
        await authentication
            .Received(1)
            .SignOutAsync(
                context.HttpContext,
                TwoFactorAuthentication.Scheme,
                Arg.Any<AuthenticationProperties?>()
            );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreatePrincipalAsyncOpenChallengeIssuesTicketThatNamesIt(bool keepSignedIn)
    {
        var principal = await IssuedTicketAsync(keepSignedIn);

        TwoFactorTicketValidator.ReadChallenge(principal).Should().Be(challenge);
        TwoFactorTicketValidator.ReadKeepSignedIn(principal).Should().Be(keepSignedIn);
        principal.Identity!.AuthenticationType.Should().Be(TwoFactorAuthentication.Scheme);
    }

    [Fact]
    public async Task CreatePrincipalAsyncClosedChallengeIssuesNoTicket()
    {
        var principal = await Build()
            .CreatePrincipalAsync(challenge, false, TestContext.Current.CancellationToken);

        principal.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsyncOpenChallengeWithCurrentStampKeepsTheTicket()
    {
        var principal = await IssuedTicketAsync();
        var context = Context(principal);

        await Build().ValidateAsync(context);

        context.Principal.Should().BeSameAs(principal);
        await authentication.DidNotReceiveWithAnyArgs().SignOutAsync(default!, default, default);
    }

    [Fact]
    public async Task ValidateAsyncStampOfAnotherPasswordRejectsTheTicket()
    {
        var context = Context(await IssuedTicketAsync());
        ChallengeIsOpen("stamp-of-the-newer-password");

        await Build().ValidateAsync(context);

        await ShouldBeRejectedAsync(context);
    }

    [Fact]
    public async Task ValidateAsyncClosedChallengeRejectsTheTicket()
    {
        var context = Context(await IssuedTicketAsync());
        tickets
            .HandleAsync(Arg.Any<GetLoginChallengeTicketQuery>(), Arg.Any<CancellationToken>())
            .Returns((LoginChallengeTicket?)null);

        await Build().ValidateAsync(context);

        await ShouldBeRejectedAsync(context);
    }

    [Theory]
    [InlineData("codigoactivo:challenge")]
    [InlineData("codigoactivo:credential")]
    [InlineData(ClaimTypes.NameIdentifier)]
    public async Task ValidateAsyncTicketMissingAClaimIsRejectedWithoutLookup(string claimType)
    {
        var context = Context(Without(await IssuedTicketAsync(), claimType));
        tickets.ClearReceivedCalls();

        await Build().ValidateAsync(context);

        await ShouldBeRejectedAsync(context);
        await tickets
            .DidNotReceiveWithAnyArgs()
            .HandleAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EndChallengeAsyncTicketClosesTheChallengeItNames()
    {
        var principal = await IssuedTicketAsync();

        await Build().EndChallengeAsync(principal, TestContext.Current.CancellationToken);

        await endChallenge
            .Received(1)
            .HandleAsync(new EndLoginChallengeCommand(challenge), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EndChallengeAsyncWithoutTicketClosesNothing()
    {
        await Build().EndChallengeAsync(null, TestContext.Current.CancellationToken);

        await endChallenge
            .DidNotReceiveWithAnyArgs()
            .HandleAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ReadKeepSignedInWithoutTicketReturnsFalse()
    {
        TwoFactorTicketValidator.ReadKeepSignedIn(null).Should().BeFalse();
    }
}
