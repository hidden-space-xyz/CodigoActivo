using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
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
        DateOnly? birthDate = null,
        Gender gender = Gender.Female,
        IReadOnlyList<RegisterMinorRequest>? minors = null
    )
    {
        return new RegisterRequest(
            firstName,
            "Nueva",
            email,
            phone,
            password,
            birthDate ?? AdultBirthDate,
            gender,
            minors
        );
    }

    private async Task<(Guid UserId, string Otp)> RegisterPendingAdultAsync(HttpClient client)
    {
        using var response = await client.PostJsonAsync(
            "/api/auth/register",
            NewAdultRequest(),
            Ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync<RegisterResponse>(Ct);
        return (body!.Adult.Id, Factory.EmailSender.LastOtpSentTo(NewAdultEmail));
    }

    [Fact]
    public async Task Csrf_Anonymous_ReturnsTokenAndSetsCookie()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/csrf", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<CsrfTokenResponse>(Ct);
        body!.Token.Should().NotBeNullOrEmpty();
        body.HeaderName.Should().Be("X-CSRF-TOKEN");
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Register_NewAdult_ReturnsCreatedSendsOtpAndPersistsPending()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync("/api/auth/register", NewAdultRequest(), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var raw = await response.Content.ReadAsStringAsync(Ct);
        var body = await response.ReadJsonAsync<RegisterResponse>(Ct);
        body!.RequiresVerification.Should().BeTrue();
        body.Minors.Should().BeEmpty();
        body.Adult.Email.Should().Be(NewAdultEmail);
        body.Adult.Status.Id.Should().Be(SeedIds.UserStatusTypes.Pending);
        body.Adult.Gender.Should().Be(Gender.Female);
        body.Adult.IsAdmin.Should().BeFalse();
        body.Adult.Type.Should().BeNull();

        var otp = Factory.EmailSender.LastOtpSentTo(NewAdultEmail);
        raw.Should()
            .NotContain($"\"{otp}\"", "the OTP must never be returned in the HTTP response");

        var stored = await FindAsync<User>(body.Adult.Id);
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Pending);
        stored.Gender.Should().Be(Gender.Female);
        stored.UserTypeId.Should().Be(SeedIds.UserTypes.Participant);
        stored.OtpCodeHash.Should().NotBeNullOrEmpty();
        stored.OtpCodeHash.Should().NotBe(otp, "the OTP must be stored hashed, not in plaintext");
        stored.OtpExpiresAt.Should().Be(Factory.Clock.UtcNow.AddMinutes(15));
        stored.OtpLastSentAt.Should().Be(Factory.Clock.UtcNow);
    }

    [Fact]
    public async Task Register_WithMinors_AssignsParticipantTypeToAdultAndMinors()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            NewAdultRequest(
                minors:
                [
                    new RegisterMinorRequest(
                        "Leo",
                        "Nueva",
                        new DateOnly(2016, 3, 10),
                        Gender.Other
                    ),
                ]
            ),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync<RegisterResponse>(Ct);
        body!.Minors.Should().HaveCount(1);
        body.Minors[0].Type.Should().BeNull();

        var storedAdult = await FindAsync<User>(body.Adult.Id);
        storedAdult!.UserTypeId.Should().Be(SeedIds.UserTypes.Participant);

        var storedMinor = await FindAsync<User>(body.Minors[0].Id);
        storedMinor!.UserTypeId.Should().Be(SeedIds.UserTypes.Participant);
        storedMinor.ParentId.Should().Be(body.Adult.Id);
        storedMinor.Gender.Should().Be(Gender.Other);
    }

    [Fact]
    public async Task Register_GenderMissing_ReturnsValidationError()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            new
            {
                firstName = "Nadia",
                lastName = "Nueva",
                email = NewAdultEmail,
                phone = "+34600000099",
                password = "Str0ngPass!",
                birthDate = "1996-01-15",
            },
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Register_MinorGenderMissing_ReturnsValidationError()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            new
            {
                firstName = "Nadia",
                lastName = "Nueva",
                email = NewAdultEmail,
                phone = "+34600000099",
                password = "Str0ngPass!",
                birthDate = "1996-01-15",
                gender = "Female",
                minors = new[]
                {
                    new
                    {
                        firstName = "Leo",
                        lastName = "Nueva",
                        birthDate = "2016-03-10",
                    },
                },
            },
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
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
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
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
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Register_BirthDateIsTheClocksToday_PassesValidation()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            NewAdultRequest(birthDate: Factory.Clock.Today),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RegisterAdultCannotBeMinor);
    }

    [Fact]
    public async Task Verify_CorrectOtp_ActivatesUser()
    {
        var client = CreateClient();
        var (userId, otp) = await RegisterPendingAdultAsync(client);

        var response = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(otp),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<UserResponse>(Ct);
        body!.Status.Id.Should().Be(SeedIds.UserStatusTypes.Active);

        var stored = await FindAsync<User>(userId);
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
            Ct
        );
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(NewAdultEmail, "Str0ngPass!"),
            Ct
        );

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.ReadJsonAsync<UserResponse>(Ct);
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
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.OtpInvalidOrExpired);

        var stored = await FindAsync<User>(userId);
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
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.OtpInvalidOrExpired);
    }

    [Fact]
    public async Task Verify_BlankOtp_ReturnsValidationError()
    {
        var client = CreateClient();
        var (userId, _) = await RegisterPendingAdultAsync(client);

        var response = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest("   "),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Verify_UnknownUser_ReturnsNotFound()
    {
        var client = CreateClient();

        var response = await client.PatchJsonAsync(
            $"/api/auth/{Guid.NewGuid()}/verify",
            new VerifyRequest("123456"),
            Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task ResendVerification_WithinCooldown_Rejected()
    {
        var client = CreateClient();
        var (userId, _) = await RegisterPendingAdultAsync(client);

        var response = await client.PostJsonAsync(
            $"/api/auth/{userId}/resend-verification",
            body: null,
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.OtpResendCooldownActive);
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
            Ct
        );

        resend.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Factory.EmailSender.Sent.Should().HaveCount(2);

        var newOtp = Factory.EmailSender.LastOtpSentTo(NewAdultEmail);
        var verify = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(newOtp),
            Ct
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
            Ct
        );
        resend.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var secondOtp = Factory.EmailSender.LastOtpSentTo(NewAdultEmail);
        secondOtp.Should().NotBe(firstOtp);

        using var stale = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(firstOtp),
            Ct
        );
        await stale.ShouldBeBadRequestAsync(ErrorCode.OtpInvalidOrExpired);

        var verify = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(secondOtp),
            Ct
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
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.OtpResendNotAllowed);
    }

    [Fact]
    public async Task Login_PendingUserVerificationRequired_ReturnsForbidden()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.PendingEmail, TestSeedData.Password),
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.UserAccountPendingVerification);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkSetsCookieAndRecordsLogin()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.AdminEmail, TestSeedData.Password),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(c => c.Contains("CodigoActivo.Session", StringComparison.Ordinal));
        var body = await response.ReadJsonAsync<UserResponse>(Ct);
        body!.Id.Should().Be(TestSeedData.Users.AdminId);

        var stored = await FindAsync<User>(TestSeedData.Users.AdminId);
        stored!.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.AdminEmail, "WrongPassword!"),
            Ct
        );

        await response.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
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

        var response = await client.SendAsync(request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.InvalidCsrfToken);
    }

    [Fact]
    public async Task Me_Authenticated_ReturnsCurrentUser()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/auth/me", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<UserResponse>(Ct);
        body!.Id.Should().Be(TestSeedData.Users.AdminId);
        body.Email.Should().Be(TestSeedData.AdminEmail);
        body.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Me_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/me", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Authenticated_ReturnsNoContentAndClearsSession()
    {
        var client = await LoginAsAdminAsync();

        var logout = await client.PostJsonAsync("/api/auth/logout", body: null, Ct);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var me = await client.GetAsync("/api/auth/me", Ct);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> RequestPasswordResetAsync(HttpClient client, string email)
    {
        using var response = await client.PostJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(email),
            Ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return Factory.EmailSender.LastOtpSentTo(email);
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_SendsResetLinkAndStoresCodeHashed()
    {
        var client = CreateClient();

        var code = await RequestPasswordResetAsync(client, TestSeedData.MemberEmail);

        Factory.EmailSender.Sent.Should().HaveCount(1);
        Factory
            .EmailSender.Sent[0]
            .TextBody.Should()
            .Contain($"/reset-password?userId={TestSeedData.Users.MemberId}");

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.PasswordResetCodeHash.Should().NotBeNullOrEmpty();
        stored
            .PasswordResetCodeHash.Should()
            .NotBe(code, "the reset code must be stored hashed, not in plaintext");
        stored.PasswordResetExpiresAt.Should().Be(Factory.Clock.UtcNow.AddMinutes(15));
        stored.PasswordResetLastSentAt.Should().Be(Factory.Clock.UtcNow);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsNoContentWithoutSending()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest("nobody@codigoactivo.test"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ForgotPassword_BlockedUser_ReturnsNoContentWithoutSending()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(TestSeedData.BlockedEmail),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ForgotPassword_InvalidEmail_ReturnsValidationError()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest("not-an-email"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task ForgotPassword_WithinCooldown_DoesNotSendSecondEmail()
    {
        var client = CreateClient();
        await RequestPasswordResetAsync(client, TestSeedData.MemberEmail);

        var response = await client.PostJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(TestSeedData.MemberEmail),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Factory.EmailSender.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task ForgotPassword_AfterCooldown_NewCodeInvalidatesPreviousOne()
    {
        var client = CreateClient();
        var firstCode = await RequestPasswordResetAsync(client, TestSeedData.MemberEmail);

        Factory.Clock.UtcNow += TimeSpan.FromSeconds(61);
        var secondCode = await RequestPasswordResetAsync(client, TestSeedData.MemberEmail);

        Factory.EmailSender.Sent.Should().HaveCount(2);
        secondCode.Should().NotBe(firstCode);

        using var stale = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(firstCode, "NuevaPass123!"),
            Ct
        );
        await stale.ShouldBeBadRequestAsync(ErrorCode.PasswordResetInvalidOrExpired);

        var reset = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(secondCode, "NuevaPass123!"),
            Ct
        );
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPasswordThenLogin_ValidCode_AllowsOnlyTheNewPassword()
    {
        var client = CreateClient();
        var code = await RequestPasswordResetAsync(client, TestSeedData.MemberEmail);

        using var reset = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(code, "NuevaPass123!"),
            Ct
        );
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var oldLogin = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.MemberEmail, TestSeedData.Password),
            Ct
        );
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var newLogin = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.MemberEmail, "NuevaPass123!"),
            Ct
        );
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await newLogin.ReadJsonAsync<UserResponse>(Ct);
        body!.Id.Should().Be(TestSeedData.Users.MemberId);

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.PasswordResetCodeHash.Should().BeNull();
        stored.PasswordResetExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task ResetPassword_ExpiredCode_Rejected()
    {
        var client = CreateClient();
        var code = await RequestPasswordResetAsync(client, TestSeedData.MemberEmail);

        Factory.Clock.UtcNow += TimeSpan.FromMinutes(16);
        var response = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(code, "NuevaPass123!"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.PasswordResetInvalidOrExpired);
    }

    [Fact]
    public async Task ResetPassword_WrongCode_RejectedWithoutConsumingTheCode()
    {
        var client = CreateClient();
        var code = await RequestPasswordResetAsync(client, TestSeedData.MemberEmail);

        using var wrong = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(Guid.NewGuid().ToString(), "NuevaPass123!"),
            Ct
        );
        await wrong.ShouldBeBadRequestAsync(ErrorCode.PasswordResetInvalidOrExpired);

        var retry = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(code, "NuevaPass123!"),
            Ct
        );
        retry.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_CodeAlreadyUsed_Rejected()
    {
        var client = CreateClient();
        var code = await RequestPasswordResetAsync(client, TestSeedData.MemberEmail);

        using var first = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(code, "NuevaPass123!"),
            Ct
        );
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var replay = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(code, "OtraPass123!"),
            Ct
        );

        await replay.ShouldBeBadRequestAsync(ErrorCode.PasswordResetInvalidOrExpired);
    }

    [Fact]
    public async Task ResetPassword_WithoutPriorRequest_Rejected()
    {
        var client = CreateClient();

        var response = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(Guid.NewGuid().ToString(), "NuevaPass123!"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.PasswordResetInvalidOrExpired);
    }

    [Fact]
    public async Task ResetPassword_UnknownUser_ReturnsNotFound()
    {
        var client = CreateClient();

        var response = await client.PatchJsonAsync(
            $"/api/auth/{Guid.NewGuid()}/reset-password",
            new ResetPasswordRequest(Guid.NewGuid().ToString(), "NuevaPass123!"),
            Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task ForgotPassword_UppercaseEmail_SendsResetCodeToTheNormalizedAddress()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(TestSeedData.MemberEmail.ToUpperInvariant()),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Factory.EmailSender.Sent.Should().HaveCount(1);
        Factory.EmailSender.LastOtpSentTo(TestSeedData.MemberEmail).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResetPassword_PendingUser_ChangesPasswordWithoutActivatingTheAccount()
    {
        var client = CreateClient();
        var code = await RequestPasswordResetAsync(client, TestSeedData.PendingEmail);

        using var reset = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.PendingId}/reset-password",
            new ResetPasswordRequest(code, "NuevaPass123!"),
            Ct
        );
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = await FindAsync<User>(TestSeedData.Users.PendingId);
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Pending);
        stored.PasswordHash.Should().Be(FakePasswordHasher.Prefix + "NuevaPass123!");
    }

    [Fact]
    public async Task ResetPassword_VerificationOtpAndResetCode_AreNotInterchangeable()
    {
        var client = CreateClient();
        var (userId, verificationOtp) = await RegisterPendingAdultAsync(client);
        var resetCode = await RequestPasswordResetAsync(client, NewAdultEmail);
        resetCode.Should().NotBe(verificationOtp);

        using var verifyWithResetCode = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(resetCode),
            Ct
        );
        await verifyWithResetCode.ShouldBeBadRequestAsync(ErrorCode.OtpInvalidOrExpired);

        using var resetWithVerificationOtp = await client.PatchJsonAsync(
            $"/api/auth/{userId}/reset-password",
            new ResetPasswordRequest(verificationOtp, "NuevaPass123!"),
            Ct
        );
        await resetWithVerificationOtp.ShouldBeBadRequestAsync(
            ErrorCode.PasswordResetInvalidOrExpired
        );

        using var verify = await client.PatchJsonAsync(
            $"/api/auth/{userId}/verify",
            new VerifyRequest(verificationOtp),
            Ct
        );
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var reset = await client.PatchJsonAsync(
            $"/api/auth/{userId}/reset-password",
            new ResetPasswordRequest(resetCode, "NuevaPass123!"),
            Ct
        );
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_ShortPassword_ReturnsValidationError()
    {
        var client = CreateClient();
        var code = await RequestPasswordResetAsync(client, TestSeedData.MemberEmail);

        var response = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(code, "corta"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }
}
