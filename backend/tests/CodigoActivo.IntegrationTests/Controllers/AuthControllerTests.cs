using System.Net;
using System.Net.Http.Json;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

/// <summary>
/// HTTP-level tests for the cookie-session auth surface: CSRF issuance, self-registration + OTP
/// verification, the login status/credential matrix, the authenticated <c>me</c>/<c>logout</c>
/// endpoints, and CSRF enforcement on unsafe verbs. Runs through the real pipeline (cookie auth,
/// CSRF middleware, controller, service, repositories) on the in-memory store.
/// </summary>
public sealed class AuthControllerTests(CodigoActivoWebAppFactory factory) : IntegrationTestBase(factory)
{
    // BirthDate.IsMinor() reads the real wall clock (not the TestClock), so anchor ages to "now".
    private static readonly DateOnly AdultBirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30);

    private static RegisterRequest NewAdultRequest(
        string email = "new.adult@codigoactivo.test",
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

    // ---- CSRF ---------------------------------------------------------------

    [Fact]
    public async Task Csrf_is_anonymous_and_returns_token_and_sets_cookie()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/csrf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<CsrfTokenResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.HeaderName.Should().Be("X-CSRF-TOKEN");
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().NotBeEmpty();
    }

    // ---- Register -----------------------------------------------------------

    [Fact]
    public async Task Register_new_adult_returns_201_with_verification_code_and_persists_pending()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync("/api/auth/register", NewAdultRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var body = await response.ReadJsonAsync<RegisterResponse>();
        body!.VerificationCode.Should().NotBeNull().And.NotBe(Guid.Empty);
        body.Minors.Should().BeEmpty();
        body.Adult.Email.Should().Be("new.adult@codigoactivo.test");
        body.Adult.Status.Id.Should().Be(SeedIds.UserStatusTypes.Pending);
        body.Adult.IsAdmin.Should().BeFalse();
        body.Adult.Type.Id.Should().Be(SeedIds.UserTypes.Member);

        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(body.Adult.Id).AsTask());
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Pending);
        stored.OtpCode.Should().Be(body.VerificationCode);
    }

    [Theory]
    [InlineData("   ", "valid@codigoactivo.test", "+34600000098", "Str0ngPass!")] // blank first name
    [InlineData("Nadia", "not-an-email", "+34600000098", "Str0ngPass!")] // invalid email
    [InlineData("Nadia", "valid@codigoactivo.test", "+34600000098", "short")] // password too short
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
            NewAdultRequest(email: email, phone: phone, password: password, firstName: firstName)
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    // ---- Verify -------------------------------------------------------------

    private static async Task<(Guid UserId, Guid Otp)> RegisterPendingAdultAsync(HttpClient client)
    {
        using var response = await client.PostJsonAsync("/api/auth/register", NewAdultRequest());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync<RegisterResponse>();
        return (body!.Adult.Id, body.VerificationCode!.Value);
    }

    [Fact]
    public async Task Verify_with_correct_otp_activates_the_user()
    {
        var client = CreateClient();
        var (userId, otp) = await RegisterPendingAdultAsync(client);

        var response = await client.PatchJsonAsync($"/api/auth/{userId}/verify?otp={otp}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<UserResponse>();
        body!.Status.Id.Should().Be(SeedIds.UserStatusTypes.Active);

        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(userId).AsTask());
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
    }

    [Fact]
    public async Task Verify_unknown_user_is_not_found()
    {
        var client = CreateClient();

        var response = await client.PatchJsonAsync($"/api/auth/{Guid.NewGuid()}/verify?otp={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserNotFound);
    }

    // ---- Login --------------------------------------------------------------

    [Fact]
    public async Task Login_with_valid_credentials_returns_200_sets_cookie_and_records_login()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.AdminEmail, TestSeedData.Password)
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(c => c.Contains("CodigoActivo.Session", StringComparison.Ordinal));
        var body = await response.ReadJsonAsync<UserResponse>();
        body!.Id.Should().Be(TestSeedData.Users.AdminId);

        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(TestSeedData.Users.AdminId).AsTask());
        stored!.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.AdminEmail, "WrongPassword!")
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
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

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.InvalidCsrfToken);
    }

    // ---- Me / Logout --------------------------------------------------------

    [Fact]
    public async Task Me_when_authenticated_returns_the_current_user()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<UserResponse>();
        body!.Id.Should().Be(TestSeedData.Users.AdminId);
        body.Email.Should().Be(TestSeedData.AdminEmail);
    }

    [Fact]
    public async Task Me_when_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_when_authenticated_returns_204_and_clears_the_session()
    {
        var client = await LoginAsAdminAsync();

        var logout = await client.PostJsonAsync("/api/auth/logout", body: null);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
