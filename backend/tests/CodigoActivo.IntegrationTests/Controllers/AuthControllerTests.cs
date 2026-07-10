using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class AuthControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string NewAdultEmail = "new.adult@codigoactivo.test";

    private static readonly DateOnly AdultBirthDate = DateOnly
        .FromDateTime(DateTime.UtcNow)
        .AddYears(-30);

    private static RegisterRequest NewAdultRequest(
        string email = NewAdultEmail,
        string phone = "+34600000099",
        string password = "Str0ngPass!",
        string firstName = "Nadia",
        DateOnly? birthDate = null
    )
    {
        return new RegisterRequest(
            firstName,
            "Nueva",
            email,
            phone,
            password,
            birthDate ?? AdultBirthDate,
            SeedIds.UserTypes.Member,
            Minors: null
        );
    }

    private async Task<(Guid UserId, string Otp)> RegisterPendingAdultAsync(HttpClient client)
    {
        using var response = await client.PostJsonAsync(
            "/api/auth/register",
            NewAdultRequest(),
            TestContext.Current.CancellationToken
        );
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync<RegisterResponse>(
            TestContext.Current.CancellationToken
        );
        return (body!.Adult.Id, Factory.EmailSender.LastOtpSentTo(NewAdultEmail));
    }

    [Fact]
    public async Task Csrf_is_anonymous_and_returns_token_and_sets_cookie()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/auth/csrf",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<CsrfTokenResponse>(
            TestContext.Current.CancellationToken
        );
        body!.Token.Should().NotBeNullOrEmpty();
        body.HeaderName.Should().Be("X-CSRF-TOKEN");
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Register_new_adult_returns_201_sends_the_otp_by_email_and_persists_pending()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            NewAdultRequest(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body = await response.ReadJsonAsync<RegisterResponse>(
            TestContext.Current.CancellationToken
        );
        body!.RequiresVerification.Should().BeTrue();
        body.Minors.Should().BeEmpty();
        body.Adult.Email.Should().Be(NewAdultEmail);
        body.Adult.Status.Id.Should().Be(SeedIds.UserStatusTypes.Pending);
        body.Adult.IsAdmin.Should().BeFalse();
        body.Adult.Type.Id.Should().Be(SeedIds.UserTypes.Member);

        var otp = Factory.EmailSender.LastOtpSentTo(NewAdultEmail);
        raw.Should()
            .NotContain($"\"{otp}\"", "the OTP must never be returned in the HTTP response");

        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync([body.Adult.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Pending);
        stored.OtpCodeHash.Should().NotBeNullOrEmpty();
        stored.OtpCodeHash.Should().NotBe(otp, "the OTP must be stored hashed, not in plaintext");
        stored.OtpExpiresAt.Should().Be(Factory.Clock.UtcNow.AddMinutes(15));
        stored.OtpLastSentAt.Should().Be(Factory.Clock.UtcNow);
    }

    [Theory]
    [InlineData("   ", "valid@codigoactivo.test", "+34600000098", "Str0ngPass!")]
    [InlineData("Nadia", "not-an-email", "+34600000098", "Str0ngPass!")]
    [InlineData("Nadia", "valid@codigoactivo.test", "+34600000098", "short")]
    public async Task Register_with_invalid_body_is_validation_error(
        string firstName,
        string email,
        string phone,
        string password
    )
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            NewAdultRequest(email: email, phone: phone, password: password, firstName: firstName),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Verify_with_correct_otp_activates_the_user()
    {
        var client = CreateClient();
        var (userId, otp) = await RegisterPendingAdultAsync(client);

        var response = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(otp),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<UserResponse>(
            TestContext.Current.CancellationToken
        );
        body!.Status.Id.Should().Be(SeedIds.UserStatusTypes.Active);

        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync([userId], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        stored.OtpCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task Verify_then_login_succeeds_end_to_end()
    {
        var client = CreateClient();
        var (userId, otp) = await RegisterPendingAdultAsync(client);

        using var verify = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(otp),
            TestContext.Current.CancellationToken
        );
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(NewAdultEmail, "Str0ngPass!"),
            TestContext.Current.CancellationToken
        );

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.ReadJsonAsync<UserResponse>(TestContext.Current.CancellationToken);
        body!.Id.Should().Be(userId);
    }

    [Fact]
    public async Task Verify_with_wrong_otp_is_rejected_and_leaves_the_account_pending()
    {
        var client = CreateClient();
        var (userId, _) = await RegisterPendingAdultAsync(client);

        var response = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(Guid.NewGuid().ToString()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.OtpInvalidOrExpired);

        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync([userId], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Pending);
        stored.OtpCodeHash.Should().NotBeNull("a wrong guess must not consume the code");
    }

    [Fact]
    public async Task Verify_with_expired_otp_is_rejected()
    {
        var client = CreateClient();
        var (userId, otp) = await RegisterPendingAdultAsync(client);

        Factory.Clock.UtcNow += TimeSpan.FromMinutes(16);
        var response = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(otp),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.OtpInvalidOrExpired);
    }

    [Fact]
    public async Task Verify_with_blank_otp_is_validation_error()
    {
        var client = CreateClient();
        var (userId, _) = await RegisterPendingAdultAsync(client);

        var response = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest("   "),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Verify_unknown_user_is_not_found()
    {
        var client = CreateClient();

        var response = await client.PatchJsonAsync(
            $"/api/auth/{Guid.NewGuid()}/verify",
            new VerifyRequest("123456"),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task Resend_verification_within_the_cooldown_is_rejected()
    {
        var client = CreateClient();
        var (userId, _) = await RegisterPendingAdultAsync(client);

        var response = await client.PostJsonAsync(
            $"/api/auth/{userId}/resend-verification",
            body: null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.OtpResendCooldownActive);
    }

    [Fact]
    public async Task Resend_verification_after_the_cooldown_sends_a_new_working_code()
    {
        var client = CreateClient();
        var (userId, _) = await RegisterPendingAdultAsync(client);

        Factory.Clock.UtcNow += TimeSpan.FromSeconds(61);
        var resend = await client.PostJsonAsync(
            $"/api/auth/{userId}/resend-verification",
            body: null,
            TestContext.Current.CancellationToken
        );

        resend.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Factory.EmailSender.Sent.Should().HaveCount(2);

        var newOtp = Factory.EmailSender.LastOtpSentTo(NewAdultEmail);
        var verify = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(newOtp),
            TestContext.Current.CancellationToken
        );
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resend_verification_invalidates_the_previously_issued_code()
    {
        var client = CreateClient();
        var (userId, firstOtp) = await RegisterPendingAdultAsync(client);

        Factory.Clock.UtcNow += TimeSpan.FromSeconds(61);
        using var resend = await client.PostJsonAsync(
            $"/api/auth/{userId}/resend-verification",
            body: null,
            TestContext.Current.CancellationToken
        );
        resend.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var secondOtp = Factory.EmailSender.LastOtpSentTo(NewAdultEmail);
        secondOtp.Should().NotBe(firstOtp);

        using var stale = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(firstOtp),
            TestContext.Current.CancellationToken
        );
        stale.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await stale.ReadJsonAsync<ApiErrorResponse>(TestContext.Current.CancellationToken))!
            .Code.Should()
            .Be(ErrorCode.OtpInvalidOrExpired);

        var verify = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(secondOtp),
            TestContext.Current.CancellationToken
        );
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resend_verification_for_an_active_user_is_rejected()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/resend-verification",
            body: null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.OtpResendNotAllowed);
    }

    [Fact]
    public async Task Login_of_a_pending_user_is_forbidden_when_verification_is_required()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.PendingEmail, TestSeedData.Password),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.UserAccountPendingVerification);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_200_sets_cookie_and_records_login()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.AdminEmail, TestSeedData.Password),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(c => c.Contains("CodigoActivo.Session", StringComparison.Ordinal));
        var body = await response.ReadJsonAsync<UserResponse>(
            TestContext.Current.CancellationToken
        );
        body!.Id.Should().Be(TestSeedData.Users.AdminId);

        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync([TestSeedData.Users.AdminId], TestContext.Current.CancellationToken)
                .AsTask()
        );
        stored!.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.AdminEmail, "WrongPassword!"),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.InvalidCredentials);
    }

    [Fact]
    public async Task Login_without_csrf_token_is_rejected()
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(
                new LoginRequest(TestSeedData.AdminEmail, TestSeedData.Password),
                options: TestJson.Options
            ),
        };

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.InvalidCsrfToken);
    }

    [Fact]
    public async Task Me_when_authenticated_returns_the_current_user()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<UserResponse>(
            TestContext.Current.CancellationToken
        );
        body!.Id.Should().Be(TestSeedData.Users.AdminId);
        body.Email.Should().Be(TestSeedData.AdminEmail);
    }

    [Fact]
    public async Task Me_when_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_when_authenticated_returns_204_and_clears_the_session()
    {
        var client = await LoginAsAdminAsync();

        var logout = await client.PostJsonAsync(
            "/api/auth/logout",
            body: null,
            TestContext.Current.CancellationToken
        );

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var me = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
