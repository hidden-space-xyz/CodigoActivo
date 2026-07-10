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

    private static readonly DateOnly AdultBirthDate = new(1996, 1, 15);

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
    public async Task Csrf_Anonymous_ReturnsTokenAndSetsCookie()
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
    public async Task Register_NewAdult_ReturnsCreatedSendsOtpAndPersistsPending()
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
    public async Task Register_InvalidBody_ReturnsValidationError(
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

    [Theory]
    [InlineData(2026, 7, 5)]
    [InlineData(2027, 1, 1)]
    [InlineData(1, 1, 1)]
    public async Task Register_BirthDateInTheFutureOrUnset_ReturnsValidationError(
        int year,
        int month,
        int day
    )
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            NewAdultRequest(birthDate: new DateOnly(year, month, day)),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Register_BirthDateIsTheClocksToday_PassesValidation()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            NewAdultRequest(birthDate: Factory.Clock.Today),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.RegisterAdultCannotBeMinor);
    }

    [Fact]
    public async Task Verify_CorrectOtp_ActivatesUser()
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
    public async Task VerifyThenLogin_ValidOtpAndCredentials_Succeeds()
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
    public async Task Verify_WrongOtp_RejectedAndAccountStaysPending()
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
    public async Task Verify_ExpiredOtp_Rejected()
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
    public async Task Verify_BlankOtp_ReturnsValidationError()
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
    public async Task Verify_UnknownUser_ReturnsNotFound()
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
    public async Task ResendVerification_WithinCooldown_Rejected()
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
    public async Task ResendVerification_AfterCooldown_SendsNewWorkingCode()
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
    public async Task ResendVerification_NewCodeIssued_InvalidatesPreviousCode()
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
    public async Task ResendVerification_ActiveUser_Rejected()
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
    public async Task Login_PendingUserVerificationRequired_ReturnsForbidden()
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
    public async Task Login_ValidCredentials_ReturnsOkSetsCookieAndRecordsLogin()
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
    public async Task Login_WrongPassword_ReturnsUnauthorized()
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
    public async Task Login_WithoutCsrfToken_ReturnsBadRequest()
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
    public async Task Me_Authenticated_ReturnsCurrentUser()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<UserResponse>(
            TestContext.Current.CancellationToken
        );
        body!.Id.Should().Be(TestSeedData.Users.AdminId);
        body.Email.Should().Be(TestSeedData.AdminEmail);
        body.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Me_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Authenticated_ReturnsNoContentAndClearsSession()
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
