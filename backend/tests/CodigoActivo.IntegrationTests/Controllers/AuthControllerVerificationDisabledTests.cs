using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class AuthControllerVerificationDisabledTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string NewAdultEmail = "new.adult@codigoactivo.test";

    private HttpClient CreateDisabledClient() => Factory.WithVerificationDisabled().CreateClient();

    private static RegisterRequest NewAdultRequest()
    {
        return new RegisterRequest(
            "Nadia",
            "Nueva",
            NewAdultEmail,
            "+34600000099",
            "Str0ngPass!",
            new DateOnly(1996, 1, 15),
            Minors: null
        );
    }

    [Fact]
    public async Task Register_VerificationDisabled_CreatesActiveAccountWithoutSendingEmail()
    {
        var client = CreateDisabledClient();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            NewAdultRequest(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync<RegisterResponse>(
            TestContext.Current.CancellationToken
        );
        body!.RequiresVerification.Should().BeFalse();
        body.Adult.Status.Id.Should().Be(SeedIds.UserStatusTypes.Active);
        Factory.EmailSender.Sent.Should().BeEmpty();

        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync([body.Adult.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        stored.OtpCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task Register_VerificationDisabled_AllowsImmediateLogin()
    {
        var client = CreateDisabledClient();
        using var register = await client.PostJsonAsync(
            "/api/auth/register",
            NewAdultRequest(),
            TestContext.Current.CancellationToken
        );
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(NewAdultEmail, "Str0ngPass!"),
            TestContext.Current.CancellationToken
        );

        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_ExistingPendingUserVerificationDisabled_ActivatesUser()
    {
        var client = CreateDisabledClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.PendingEmail, TestSeedData.Password),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync(
                    [TestSeedData.Users.PendingId],
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
    }

    [Fact]
    public async Task Login_ExistingPendingUserVerificationDisabled_StampsClockTimesOnTheSelfHeal()
    {
        var client = CreateDisabledClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.PendingEmail, TestSeedData.Password),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync(
                    [TestSeedData.Users.PendingId],
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );
        stored!.UpdatedAt.Should().Be(Factory.Clock.UtcNow);
        stored.LastLoginAt.Should().Be(Factory.Clock.UtcNow);
    }

    [Fact]
    public async Task ResendVerification_VerificationDisabled_IsRejected()
    {
        var client = CreateDisabledClient();

        var response = await client.PostJsonAsync(
            $"/api/auth/{TestSeedData.Users.PendingId}/resend-verification",
            body: null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.OtpResendNotAllowed);
    }
}
