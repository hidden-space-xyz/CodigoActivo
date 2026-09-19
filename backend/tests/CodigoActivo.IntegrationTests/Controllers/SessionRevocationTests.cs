using System.Net;
using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class SessionRevocationTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private async Task<string> ForgeSessionCookieAsync(ClaimsPrincipal principal)
    {
        var monitor = Factory.Services.GetRequiredService<
            IOptionsMonitor<CookieAuthenticationOptions>
        >();
        var options = monitor.Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(
            principal,
            CookieAuthenticationDefaults.AuthenticationScheme
        );
        var protectedTicket = options.TicketDataFormat.Protect(ticket);
        return $"{options.Cookie.Name}={protectedTicket}";
    }

    private static ClaimsPrincipal PrincipalWithClaims(params Claim[] claims)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
        );
    }

    private async Task<HttpClient> ClientWithForgedCookieAsync(ClaimsPrincipal principal)
    {
        var cookie = await ForgeSessionCookieAsync(principal);
        var client = Factory.CreateDefaultClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    private Task<List<UserSession>> SessionsForAsync(Guid userId)
    {
        return Factory.QueryAsync(db =>
            db.Set<UserSession>().Where(s => s.UserId == userId).ToListAsync(Ct)
        );
    }

    [Fact]
    public async Task MeTicketWithoutSidClaimIsRejected()
    {
        var client = await ClientWithForgedCookieAsync(
            PrincipalWithClaims(
                new Claim(ClaimTypes.NameIdentifier, TestSeedData.Users.AdminId.ToString()),
                new Claim("codigoactivo:credential", "anything")
            )
        );

        var response = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MeTicketWithUnparseableSidIsRejected()
    {
        var client = await ClientWithForgedCookieAsync(
            PrincipalWithClaims(
                new Claim(ClaimTypes.NameIdentifier, TestSeedData.Users.AdminId.ToString()),
                new Claim("codigoactivo:credential", "anything"),
                new Claim("sid", "not-a-valid-guid")
            )
        );

        var response = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MeSessionRowReassignedToAnotherUserIsRejected()
    {
        var client = await LoginAsMemberAsync();
        var session = (await SessionsForAsync(TestSeedData.Users.MemberId))
            .Should()
            .ContainSingle()
            .Subject;

        await Factory.SeedAsync(async db =>
        {
            var row = await db.Set<UserSession>().SingleAsync(s => s.Id == session.Id, Ct);
            row.UserId = TestSeedData.Users.AdminId;
        });

        var response = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MeSessionRowDeletedIsRejected()
    {
        var client = await LoginAsMemberAsync();

        await Factory.SeedAsync(async db =>
        {
            var rows = await db.Set<UserSession>()
                .Where(s => s.UserId == TestSeedData.Users.MemberId)
                .ToListAsync(Ct);
            db.Set<UserSession>().RemoveRange(rows);
        });

        var response = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MeSessionRowExpiredIsRejected()
    {
        var client = await LoginAsMemberAsync();

        Factory.Clock.UtcNow += SessionLifetimeOptions.DefaultLifetime + TimeSpan.FromMinutes(1);

        var response = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginPurgesTheCallersExpiredSessionsButLeavesOtherUsersLiveOnesAlone()
    {
        await LoginAsAdminAsync();
        await LoginAsMemberAsync();

        Factory.Clock.UtcNow += SessionLifetimeOptions.DefaultLifetime + TimeSpan.FromMinutes(1);

        await LoginAsMemberAsync();

        var memberSessions = await SessionsForAsync(TestSeedData.Users.MemberId);
        memberSessions
            .Should()
            .HaveCount(1, "the expired row was purged when the user logged in again");
        memberSessions[0].ExpiresAt.Should().BeAfter(Factory.Clock.UtcNow);

        var adminSessions = await SessionsForAsync(TestSeedData.Users.AdminId);
        adminSessions
            .Should()
            .HaveCount(
                1,
                "another user's expired row is untouched by someone else's login, even though it is also expired"
            );
    }

    [Fact]
    public async Task ChangePasswordRevokesEveryExistingSessionOfTheUserAndTheOldCookieIsRejected()
    {
        var client = await LoginAsMemberAsync();

        var change = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/password",
            new ChangePasswordRequest(TestSeedData.Password, "BrandNewPass123!"),
            Ct
        );
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SessionsForAsync(TestSeedData.Users.MemberId)).Should().BeEmpty();

        var me = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPasswordRevokesEveryExistingSessionOfTheUserAndTheOldCookieIsRejected()
    {
        var client = await LoginAsMemberAsync();

        using var forgot = await client.PostJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(TestSeedData.MemberEmail),
            Ct
        );
        forgot.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var code = Factory.EmailSender.LastOtpSentTo(TestSeedData.MemberEmail);

        var reset = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(code, "BrandNewPass123!"),
            Ct
        );
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SessionsForAsync(TestSeedData.Users.MemberId)).Should().BeEmpty();

        var me = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccountDeletionByAnAdministratorCascadesItsSessionsAndTheOldCookieIsRejected()
    {
        var memberClient = await LoginAsMemberAsync();
        var adminClient = await LoginAsAdminAsync();
        (await SessionsForAsync(TestSeedData.Users.MemberId)).Should().ContainSingle();

        var delete = await adminClient.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            Ct
        );
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SessionsForAsync(TestSeedData.Users.MemberId)).Should().BeEmpty();

        var me = await memberClient.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MeAfterAClaimsRefreshReissuesAPersistentCookieAndKeepsTheRowExpiry()
    {
        var client = await LoginAsMemberAsync();
        var opened = (await SessionsForAsync(TestSeedData.Users.MemberId))
            .Should()
            .ContainSingle()
            .Subject;
        await Factory.SeedAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == TestSeedData.Users.MemberId, Ct);
            user.FirstName = "Martita";
        });

        using var response = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reissued = response
            .Headers.GetValues("Set-Cookie")
            .Where(value => value.StartsWith("CodigoActivo.Session=", StringComparison.Ordinal))
            .ToList();
        reissued.Should().ContainSingle("the refreshed claims are written back to the cookie");
        SetCookieHeaderValue
            .Parse(reissued[0])
            .Expires.Should()
            .NotBeNull("the re-issued cookie stays persistent");
        (await SessionsForAsync(TestSeedData.Users.MemberId))
            .Should()
            .ContainSingle()
            .Which.ExpiresAt.Should()
            .Be(opened.ExpiresAt, "the absolute expiry of the session row never slides");
    }

    [Fact]
    public async Task LogoutWithTheSessionRowAlreadyGoneStillReturnsNoContent()
    {
        var client = await LoginAsMemberAsync();
        await Factory.SeedAsync(async db =>
        {
            var rows = await db.Set<UserSession>()
                .Where(s => s.UserId == TestSeedData.Users.MemberId)
                .ToListAsync(Ct);
            db.Set<UserSession>().RemoveRange(rows);
        });

        var logout = await client.PostJsonAsync("/api/auth/logout", body: null, Ct);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
