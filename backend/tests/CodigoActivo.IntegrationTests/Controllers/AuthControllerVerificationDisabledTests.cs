using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class AuthControllerVerificationDisabledTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string NewAdultEmail = "new.adult@codigoactivo.test";

    private HttpClient CreateDisabledClient()
    {
        return Factory.WithVerificationDisabled().CreateClient();
    }

    private static RegisterRequest NewAdultRequest()
    {
        return new RegisterRequest(
            "Nadia",
            "Nueva",
            NewAdultEmail,
            "+34600000099",
            "Str0ngPass!",
            new DateOnly(1996, 1, 15),
            Gender.Female,
            Minors: null
        );
    }

    [Fact]
    public async Task RegisterVerificationDisabledCreatesActiveAccountWithoutSendingEmail()
    {
        var client = CreateDisabledClient();

        var response = await client.PostJsonAsync("/api/auth/register", NewAdultRequest(), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync<RegisterResponse>(Ct);
        body!.RequiresVerification.Should().BeFalse();
        body.Adult.Status.Id.Should().Be(SeedIds.UserStatusTypes.Active);
        Factory.EmailSender.Sent.Should().BeEmpty();

        var stored = await FindAsync<User>(body.Adult.Id);
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        stored.OtpCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task RegisterVerificationDisabledAllowsImmediateLogin()
    {
        var client = CreateDisabledClient();
        using var register = await client.PostJsonAsync(
            "/api/auth/register",
            NewAdultRequest(),
            Ct
        );
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(NewAdultEmail, "Str0ngPass!"),
            Ct
        );

        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginExistingPendingUserVerificationDisabledActivatesAndStampsClockTimes()
    {
        var client = CreateDisabledClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.PendingEmail, TestSeedData.Password),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<User>(TestSeedData.Users.PendingId);
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        stored.UpdatedAt.Should().Be(Factory.Clock.UtcNow);
        stored.LastLoginAt.Should().Be(Factory.Clock.UtcNow);
    }

    [Fact]
    public async Task ResendVerificationVerificationDisabledIsRejected()
    {
        var client = CreateDisabledClient();

        var response = await client.PostJsonAsync(
            $"/api/auth/{TestSeedData.Users.PendingId}/resend-verification",
            body: null,
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.OtpResendNotAllowed);
    }
}
