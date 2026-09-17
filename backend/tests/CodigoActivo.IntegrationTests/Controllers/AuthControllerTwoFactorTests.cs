using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class AuthControllerTwoFactorTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string TwoFactorUrl = "/api/auth/login/two-factor";
    private const string ResendUrl = "/api/auth/login/two-factor/resend";
    private const string SetupUrl = "/api/auth/two-factor/authenticator/setup";
    private const string ConfirmUrl = "/api/auth/two-factor/authenticator/confirm";
    private const string EmailMethodUrl = "/api/auth/two-factor/email";
    private const string Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    private string CurrentCode(string secret = Secret, int stepOffset = 0)
    {
        return TotpService.ComputeCode(secret, TotpService.StepOf(Factory.Clock.UtcNow) + stepOffset);
    }

    private async Task<HttpClient> StartMemberChallengeAsync()
    {
        var client = CreateClient();
        await PassPasswordStepAsync(client, TestSeedData.MemberCredentials);
        return client;
    }

    private static Task<HttpResponseMessage> PresentAsync(HttpClient client, string code)
    {
        return client.PostJsonAsync(TwoFactorUrl, new TwoFactorLoginRequest(code), Ct);
    }

    private Task SeedAuthenticatorAsync(Guid userId, string secret = Secret, long? lastUsedStep = null)
    {
        return Factory.SeedAsync(async db =>
        {
            var user = await db.Users.FindAsync([userId], Ct);
            user!.TwoFactorMethod = TwoFactorMethod.Authenticator;
            user.AuthenticatorKey = FakeSecretProtector.Prefix + secret;
            user.AuthenticatorLastUsedStep = lastUsedStep;
        });
    }

    [Fact]
    public async Task TwoFactorCorrectEmailCodeOpensSessionRecordsLoginAndConsumesTheCode()
    {
        var client = await StartMemberChallengeAsync();
        var code = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);

        var response = await PresentAsync(client, code);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(c => c.Contains("CodigoActivo.Session=", StringComparison.Ordinal));
        var body = await response.ReadJsonAsync<UserResponse>(Ct);
        body!.Id.Should().Be(TestSeedData.Users.MemberId);
        body.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);

        using var me = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.LastLoginAt.Should().Be(Factory.Clock.UtcNow);
        stored.LoginCodeHash.Should().BeNull();

        using var replay = await PresentAsync(client, code);
        await replay.ShouldBeUnauthorizedAsync(ErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task TwoFactorWithoutPasswordStepReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await PresentAsync(client, "123456");

        await response.ShouldBeUnauthorizedAsync(ErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task TwoFactorWrongCodeReturnsBadRequestAndTheRightCodeStillWorks()
    {
        var client = await StartMemberChallengeAsync();
        var code = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);

        using var wrong = await PresentAsync(client, "000000");
        await wrong.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.TwoFactorFailedAttempts.Should().Be(1);
        stored.LoginCodeHash.Should().NotBeNull("a wrong guess must not consume the code");

        var right = await PresentAsync(client, code);
        right.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TwoFactorBlankCodeReturnsValidationError()
    {
        var client = await StartMemberChallengeAsync();

        var response = await PresentAsync(client, "   ");

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task TwoFactorExpiredEmailCodeIsRejected()
    {
        var client = await StartMemberChallengeAsync();
        var code = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);

        Factory.Clock.UtcNow += TimeSpan.FromMinutes(11);
        var response = await PresentAsync(client, code);

        await response.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);
    }

    [Fact]
    public async Task TwoFactorRepeatedWrongCodesLockTheSecondFactorUntilTheLockoutEnds()
    {
        var client = await StartMemberChallengeAsync();
        var code = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var wrong = await PresentAsync(client, "000000");
            await wrong.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);
        }

        using var locked = await PresentAsync(client, code);
        await locked.ShouldBeForbiddenAsync(ErrorCode.TwoFactorLocked);

        using var lockedLogin = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.MemberEmail, TestSeedData.Password),
            Ct
        );
        await lockedLogin.ShouldBeForbiddenAsync(ErrorCode.TwoFactorLocked);

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.TwoFactorLockedUntil.Should().Be(Factory.Clock.UtcNow.AddMinutes(15));
        stored.LoginCodeHash.Should().BeNull("locking discards the challenged code");

        Factory.Clock.UtcNow += TimeSpan.FromMinutes(16);
        var signedIn = await LoginAsMemberAsync();
        using var me = await signedIn.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TwoFactorChallengeDescribesThePendingMethodOnlyWhileTheChallengeIsOpen()
    {
        var client = await StartMemberChallengeAsync();

        using var pending = await client.GetAsync(TestUri.Rel(TwoFactorUrl), Ct);
        pending.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await pending.ReadJsonAsync<LoginChallengeResponse>(Ct);
        body.Should().Be(new LoginChallengeResponse(TwoFactorMethod.Email, "m***@codigoactivo.test"));

        await CompleteTwoFactorAsync(client, Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail));

        var closed = await client.GetAsync(TestUri.Rel(TwoFactorUrl), Ct);
        await closed.ShouldBeUnauthorizedAsync(ErrorCode.TwoFactorChallengeExpired);

        var anonymous = await CreateClient().GetAsync(TestUri.Rel(TwoFactorUrl), Ct);
        await anonymous.ShouldBeUnauthorizedAsync(ErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task LoginRepeatedWithinCooldownReusesTheEmailedCode()
    {
        var first = await StartMemberChallengeAsync();
        var code = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);

        var second = await StartMemberChallengeAsync();

        Factory.EmailSender.Sent.Should().ContainSingle("a second password step must not spam the inbox");
        var completed = await PresentAsync(second, code);
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        first.Dispose();
    }

    [Fact]
    public async Task TwoFactorResendWithinCooldownIsRejected()
    {
        var client = await StartMemberChallengeAsync();

        var response = await client.PostJsonAsync(ResendUrl, body: null, Ct);

        await response.ShouldBeConflictAsync(ErrorCode.TwoFactorResendCooldownActive);
        Factory.EmailSender.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task TwoFactorResendAfterCooldownSendsNewCodeAndInvalidatesThePreviousOne()
    {
        var client = await StartMemberChallengeAsync();
        var firstCode = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);

        Factory.Clock.UtcNow += TimeSpan.FromSeconds(61);
        using var resend = await client.PostJsonAsync(ResendUrl, body: null, Ct);
        resend.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondCode = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);
        Factory.EmailSender.Sent.Should().HaveCount(2);
        using var stale = await PresentAsync(client, firstCode);
        await stale.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);
        var fresh = await PresentAsync(client, secondCode);
        fresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TwoFactorResendWithoutChallengeReturnsUnauthorized()
    {
        var response = await CreateClient().PostJsonAsync(ResendUrl, body: null, Ct);

        await response.ShouldBeUnauthorizedAsync(ErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task TwoFactorPasswordChangedAfterThePasswordStepInvalidatesTheChallenge()
    {
        var client = await StartMemberChallengeAsync();
        var code = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);
        await Factory.SeedAsync(async db =>
        {
            var user = await db.Users.FindAsync([TestSeedData.Users.MemberId], Ct);
            user!.PasswordHash = FakePasswordHasher.Prefix + "A-Different-Password";
        });

        var response = await PresentAsync(client, code);

        await response.ShouldBeUnauthorizedAsync(ErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task TwoFactorAccountBlockedAfterThePasswordStepInvalidatesTheChallenge()
    {
        var client = await StartMemberChallengeAsync();
        var code = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);
        await Factory.SeedAsync(async db =>
        {
            var user = await db.Users.FindAsync([TestSeedData.Users.MemberId], Ct);
            user!.UserStatusTypeId = SeedIds.UserStatusTypes.Blocked;
        });

        var response = await PresentAsync(client, code);

        await response.ShouldBeUnauthorizedAsync(ErrorCode.TwoFactorChallengeExpired);
    }

    [Fact]
    public async Task LoginAuthenticatorUserOpensChallengeWithoutEmailAndAcceptsTheAppCode()
    {
        await SeedAuthenticatorAsync(TestSeedData.Users.MemberId);
        var client = CreateClient();

        var challenge = await PassPasswordStepAsync(client, TestSeedData.MemberCredentials);

        challenge.Should().Be(new LoginChallengeResponse(TwoFactorMethod.Authenticator, null));
        Factory.EmailSender.Sent.Should().BeEmpty();

        using var resend = await client.PostJsonAsync(ResendUrl, body: null, Ct);
        await resend.ShouldBeConflictAsync(ErrorCode.TwoFactorResendNotAllowed);

        var body = await CompleteTwoFactorAsync(client, CurrentCode());
        body.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.AuthenticatorLastUsedStep.Should().Be(TotpService.StepOf(Factory.Clock.UtcNow));
    }

    [Fact]
    public async Task TwoFactorAuthenticatorCodeCannotBeReplayedButTheNextOneWorks()
    {
        var used = TotpService.StepOf(Factory.Clock.UtcNow);
        await SeedAuthenticatorAsync(TestSeedData.Users.MemberId, lastUsedStep: used);
        var client = CreateClient();
        await PassPasswordStepAsync(client, TestSeedData.MemberCredentials);

        using var replayed = await PresentAsync(client, CurrentCode());
        await replayed.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);

        Factory.Clock.UtcNow += TimeSpan.FromSeconds(30);
        var next = await PresentAsync(client, CurrentCode());
        next.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TwoFactorAuthenticatorToleratesOneStepOfClockDrift()
    {
        await SeedAuthenticatorAsync(TestSeedData.Users.MemberId);
        var client = CreateClient();
        await PassPasswordStepAsync(client, TestSeedData.MemberCredentials);

        var response = await PresentAsync(client, CurrentCode(stepOffset: -1));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthenticatorSetupThenConfirmSwitchesTheSecondFactorForFutureLogins()
    {
        var client = await LoginAsMemberAsync();

        using var wrongPassword = await client.PostJsonAsync(
            SetupUrl,
            new AuthenticatorSetupRequest("WrongPassword!"),
            Ct
        );
        await wrongPassword.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);

        using var setup = await client.PostJsonAsync(
            SetupUrl,
            new AuthenticatorSetupRequest(TestSeedData.Password),
            Ct
        );
        setup.StatusCode.Should().Be(HttpStatusCode.OK);
        var enrollment = (await setup.ReadJsonAsync<AuthenticatorSetupResponse>(Ct))!;
        var secret = enrollment.SharedKey.Replace(" ", string.Empty, StringComparison.Ordinal);
        secret.Should().HaveLength(32);
        enrollment.AuthenticatorUri.Should().StartWith("otpauth://totp/").And.Contain($"secret={secret}");

        var pending = await FindAsync<User>(TestSeedData.Users.MemberId);
        pending!.PendingAuthenticatorKey.Should().Be(FakeSecretProtector.Prefix + secret);
        pending.TwoFactorMethod.Should().Be(TwoFactorMethod.Email, "the enrollment is not confirmed yet");

        using var wrongCode = await client.PostJsonAsync(
            ConfirmUrl,
            new ConfirmAuthenticatorRequest("000000"),
            Ct
        );
        await wrongCode.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);

        using var confirm = await client.PostJsonAsync(
            ConfirmUrl,
            new ConfirmAuthenticatorRequest(CurrentCode(secret)),
            Ct
        );
        confirm.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var enabled = await FindAsync<User>(TestSeedData.Users.MemberId);
        enabled!.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
        enabled.AuthenticatorKey.Should().Be(FakeSecretProtector.Prefix + secret);
        enabled.PendingAuthenticatorKey.Should().BeNull();

        using var me = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        (await me.ReadJsonAsync<UserResponse>(Ct))!.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);

        Factory.EmailSender.Clear();
        Factory.Clock.UtcNow += TimeSpan.FromSeconds(30);
        var again = CreateClient();
        var challenge = await PassPasswordStepAsync(again, TestSeedData.MemberCredentials);
        challenge.Method.Should().Be(TwoFactorMethod.Authenticator);
        Factory.EmailSender.Sent.Should().BeEmpty();
        var signedIn = await CompleteTwoFactorAsync(again, CurrentCode(secret));
        signedIn.Id.Should().Be(TestSeedData.Users.MemberId);
    }

    [Fact]
    public async Task AuthenticatorConfirmWithoutSetupReturnsBadRequest()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PostJsonAsync(
            ConfirmUrl,
            new ConfirmAuthenticatorRequest("123456"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.AuthenticatorSetupExpired);
    }

    [Fact]
    public async Task AuthenticatorConfirmAfterSetupLifetimeReturnsBadRequest()
    {
        var client = await LoginAsMemberAsync();
        using var setup = await client.PostJsonAsync(
            SetupUrl,
            new AuthenticatorSetupRequest(TestSeedData.Password),
            Ct
        );
        var secret = (await setup.ReadJsonAsync<AuthenticatorSetupResponse>(Ct))!
            .SharedKey.Replace(" ", string.Empty, StringComparison.Ordinal);

        Factory.Clock.UtcNow += TimeSpan.FromMinutes(16);
        var response = await client.PostJsonAsync(
            ConfirmUrl,
            new ConfirmAuthenticatorRequest(CurrentCode(secret)),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.AuthenticatorSetupExpired);
    }

    [Fact]
    public async Task AuthenticatorSetupAnonymousReturnsUnauthorized()
    {
        var response = await CreateClient().PostJsonAsync(
            SetupUrl,
            new AuthenticatorSetupRequest(TestSeedData.Password),
            Ct
        );

        await response.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);
    }

    [Fact]
    public async Task EmailMethodRequiresPasswordAndAppCodeAndThenEmailsCodesAgain()
    {
        await SeedAuthenticatorAsync(TestSeedData.Users.MemberId);
        var client = CreateClient();
        await PassPasswordStepAsync(client, TestSeedData.MemberCredentials);
        await CompleteTwoFactorAsync(client, CurrentCode());
        Factory.Clock.UtcNow += TimeSpan.FromSeconds(30);

        using var wrongPassword = await client.PostJsonAsync(
            EmailMethodUrl,
            new DisableAuthenticatorRequest("WrongPassword!", CurrentCode()),
            Ct
        );
        await wrongPassword.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);

        using var wrongCode = await client.PostJsonAsync(
            EmailMethodUrl,
            new DisableAuthenticatorRequest(TestSeedData.Password, "000000"),
            Ct
        );
        await wrongCode.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .TwoFactorMethod.Should()
            .Be(TwoFactorMethod.Authenticator);

        using var disabled = await client.PostJsonAsync(
            EmailMethodUrl,
            new DisableAuthenticatorRequest(TestSeedData.Password, CurrentCode()),
            Ct
        );
        disabled.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        stored.AuthenticatorKey.Should().BeNull();

        using var repeated = await client.PostJsonAsync(
            EmailMethodUrl,
            new DisableAuthenticatorRequest(TestSeedData.Password, CurrentCode()),
            Ct
        );
        await repeated.ShouldBeConflictAsync(ErrorCode.AuthenticatorNotEnabled);

        Factory.EmailSender.Clear();
        var again = CreateClient();
        var challenge = await PassPasswordStepAsync(again, TestSeedData.MemberCredentials);
        challenge.Method.Should().Be(TwoFactorMethod.Email);
        Factory.EmailSender.Sent.Should().ContainSingle();
        await CompleteTwoFactorAsync(again, Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail));
        using var me = await again.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LogoutClearsAnOpenChallengeAsWell()
    {
        var client = await LoginAsMemberAsync();
        using var login = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.MemberEmail, TestSeedData.Password),
            Ct
        );
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        using var logout = await client.PostJsonAsync("/api/auth/logout", body: null, Ct);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var challenge = await client.GetAsync(TestUri.Rel(TwoFactorUrl), Ct);
        await challenge.ShouldBeUnauthorizedAsync(ErrorCode.TwoFactorChallengeExpired);
    }
}
