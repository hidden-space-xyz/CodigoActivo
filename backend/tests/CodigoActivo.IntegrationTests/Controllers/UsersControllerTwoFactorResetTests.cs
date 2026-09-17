using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class UsersControllerTwoFactorResetTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static string ResetUrl(Guid userId)
    {
        return $"/api/users/{userId}/two-factor/reset";
    }

    private Task SeedLockedAuthenticatorUserAsync()
    {
        return Factory.SeedAsync(async db =>
        {
            var user = await db.Users.FindAsync([TestSeedData.Users.MemberId], Ct);
            user!.TwoFactorMethod = TwoFactorMethod.Authenticator;
            user.AuthenticatorKey = FakeSecretProtector.Prefix + "SECRET";
            user.TwoFactorLockedUntil = Factory.Clock.UtcNow.AddMinutes(10);
            user.TwoFactorFailedAttempts = 2;
        });
    }

    [Fact]
    public async Task ResetTwoFactorAdminWithPasswordReturnsUserToEmailAndUnlocks()
    {
        await SeedLockedAuthenticatorUserAsync();
        var admin = await LoginAsAdminAsync();

        using var response = await admin.PostJsonAsync(
            ResetUrl(TestSeedData.Users.MemberId),
            new ResetTwoFactorRequest(TestSeedData.Password),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        stored.AuthenticatorKey.Should().BeNull();
        stored.TwoFactorLockedUntil.Should().BeNull();
        stored.TwoFactorFailedAttempts.Should().Be(0);

        var member = await LoginAsMemberAsync();
        using var me = await member.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetTwoFactorWrongAdminPasswordReturnsBadRequestAndChangesNothing()
    {
        await SeedLockedAuthenticatorUserAsync();
        var admin = await LoginAsAdminAsync();

        var response = await admin.PostJsonAsync(
            ResetUrl(TestSeedData.Users.MemberId),
            new ResetTwoFactorRequest("WrongPassword!"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
    }

    [Fact]
    public async Task ResetTwoFactorBlankPasswordReturnsValidationError()
    {
        var admin = await LoginAsAdminAsync();

        var response = await admin.PostJsonAsync(
            ResetUrl(TestSeedData.Users.MemberId),
            new ResetTwoFactorRequest("  "),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task ResetTwoFactorUnknownUserReturnsNotFound()
    {
        var admin = await LoginAsAdminAsync();

        var response = await admin.PostJsonAsync(
            ResetUrl(Guid.NewGuid()),
            new ResetTwoFactorRequest(TestSeedData.Password),
            Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task ResetTwoFactorNonAdminReturnsForbidden()
    {
        await SeedLockedAuthenticatorUserAsync();
        var admin = await LoginAsAdminAsync();
        using var reset = await admin.PostJsonAsync(
            ResetUrl(TestSeedData.Users.MemberId),
            new ResetTwoFactorRequest(TestSeedData.Password),
            Ct
        );
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var member = await LoginAsMemberAsync();

        var response = await member.PostJsonAsync(
            ResetUrl(TestSeedData.Users.AdminId),
            new ResetTwoFactorRequest(TestSeedData.Password),
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.AccessDenied);
    }

    [Fact]
    public async Task ResetTwoFactorAnonymousReturnsUnauthorized()
    {
        var response = await CreateClient().PostJsonAsync(
            ResetUrl(TestSeedData.Users.MemberId),
            new ResetTwoFactorRequest(TestSeedData.Password),
            Ct
        );

        await response.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);
    }
}
