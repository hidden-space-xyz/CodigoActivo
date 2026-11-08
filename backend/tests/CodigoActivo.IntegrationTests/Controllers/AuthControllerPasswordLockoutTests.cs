using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.API.Errors;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class AuthControllerPasswordLockoutTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string LoginUrl = "/api/auth/login";
    private const string WrongPassword = "Definitely-Not-The-Password";
    private const string NewPassword = "NuevaPass123!";
    private const int Threshold = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private static Task<HttpResponseMessage> AttemptAsync(HttpClient client, string password)
    {
        return client.PostJsonAsync(
            LoginUrl,
            new LoginRequest(TestSeedData.MemberEmail, password),
            Ct
        );
    }

    private static async Task LockMemberAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < Threshold; attempt++)
        {
            using var response = await AttemptAsync(client, WrongPassword);
            await response.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
        }
    }

    private async Task<string> RequestResetCodeAsync(HttpClient client)
    {
        using var response = await client.PostJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(TestSeedData.MemberEmail),
            Ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return Factory.EmailSender.LastOtpSentTo(TestSeedData.MemberEmail);
    }

    private static async Task<HttpResponseMessage> SendPreparedAttemptAsync(
        HttpClient client,
        string csrfToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, LoginUrl);
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        request.Content = JsonContent.Create(
            new LoginRequest(TestSeedData.MemberEmail, WrongPassword),
            options: TestJson.Options
        );
        return await client.SendAsync(request, Ct);
    }

    private async Task SendParallelAttemptsAsync(int attempts)
    {
        var clients = new List<HttpClient>();
        var tokens = new List<string>();
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var client = CreateClient();
            clients.Add(client);
            tokens.Add(await client.FetchCsrfTokenAsync(Ct));
        }

        var responses = await Task.WhenAll(
            clients.Select((client, attempt) => SendPreparedAttemptAsync(client, tokens[attempt]))
        );
        foreach (var response in responses)
        {
            await response.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
            response.Dispose();
        }

        foreach (var client in clients)
        {
            client.Dispose();
        }
    }

    private bool MemberLocked(User stored)
    {
        return stored.PasswordLockout.IsLocked(Factory.Clock.UtcNow);
    }

    private Task<int> MemberSessionsAsync()
    {
        return Factory.QueryAsync(db =>
            db.UserSessions.CountAsync(
                row => row.UserId == UserId.From(TestSeedData.Users.MemberId),
                Ct
            )
        );
    }

    [Fact]
    public async Task LoginParallelWrongPasswordsCountEveryAttemptOnceAndAlertOnlyOnce()
    {
        await SendParallelAttemptsAsync(Threshold);

        var locked = await FindAsync<User>(TestSeedData.Users.MemberId);
        MemberLocked(locked!).Should().BeTrue("no attempt may be lost");
        locked!.PasswordLockout.FailedAttempts.Should().Be(0);

        await SendParallelAttemptsAsync(3);

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.PasswordLockout.Should().Be(locked.PasswordLockout, "a lock counts nothing more");
        Factory
            .EmailSender.Sent.Should()
            .ContainSingle(message => message.Kind == EmailKind.SecurityAlert);
    }

    [Fact]
    public async Task LoginFiveWrongPasswordsLocksTheAccountAndCountsEveryAttempt()
    {
        var client = CreateClient();

        for (var attempt = 1; attempt <= Threshold; attempt++)
        {
            using var response = await AttemptAsync(client, WrongPassword);
            await response.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);

            var progress = await FindAsync<User>(TestSeedData.Users.MemberId);
            progress!.PasswordLockout.FailedAttempts.Should().Be(attempt < Threshold ? attempt : 0);
            MemberLocked(progress).Should().Be(attempt >= Threshold);
        }

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.PasswordLockout.LockedUntil.Should().Be(Factory.Clock.UtcNow + LockoutDuration);
    }

    [Fact]
    public async Task LoginLockedAccountAnswersTheCorrectPasswordLikeAWrongOne()
    {
        var client = CreateClient();
        await LockMemberAsync(client);

        using var correct = await AttemptAsync(client, TestSeedData.Password);
        await correct.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);

        using var wrong = await AttemptAsync(client, WrongPassword);
        await wrong.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);

        Factory
            .EmailSender.Sent.Should()
            .NotContain(message => message.Kind == EmailKind.TwoFactorCode);
    }

    [Fact]
    public async Task LoginLockExpiresAfterItsDurationAndTheCountStartsAgain()
    {
        var client = CreateClient();
        await LockMemberAsync(client);

        Factory.Clock.UtcNow += LockoutDuration - TimeSpan.FromSeconds(1);
        using var stillLocked = await AttemptAsync(client, TestSeedData.Password);
        await stillLocked.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);

        Factory.Clock.UtcNow += TimeSpan.FromSeconds(1);
        using var wrong = await AttemptAsync(client, WrongPassword);
        await wrong.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);

        var counted = await FindAsync<User>(TestSeedData.Users.MemberId);
        counted!.PasswordLockout.FailedAttempts.Should().Be(1);
        MemberLocked(counted).Should().BeFalse();

        using var accepted = await AttemptAsync(client, TestSeedData.Password);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginLockingTheAccountKeepsItsOpenSessionsAndWarnsItsOwner()
    {
        var signedIn = await LoginAsMemberAsync();
        var sessions = await MemberSessionsAsync();

        await LockMemberAsync(CreateClient());

        using var after = await signedIn.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
        (await MemberSessionsAsync()).Should().Be(sessions).And.BePositive();

        var alert = Factory
            .EmailSender.Sent.Should()
            .ContainSingle(message => message.Kind == EmailKind.SecurityAlert)
            .Subject;
        alert.ToAddress.Should().Be(TestSeedData.MemberEmail);
        alert.Subject.Should().Be(AppStrings.EmailsSecurityAlertPasswordLockedSubject);
        alert.TextBody.Should().Contain(AppStrings.EmailsSecurityAlertPasswordLocked);
        alert.TextBody.Should().NotContain(TestSeedData.Password).And.NotContain(WrongPassword);
    }

    [Fact]
    public async Task TwoFactorChallengeOpenedBeforeTheLockCannotBeCompleted()
    {
        var pending = CreateClient();
        await PassPasswordStepAsync(pending, TestSeedData.MemberCredentials);
        var code = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);

        await LockMemberAsync(CreateClient());

        using var response = await pending.PostJsonAsync(
            "/api/auth/login/two-factor",
            new TwoFactorLoginRequest(code),
            Ct
        );

        await response.ShouldBeUnauthorizedAsync(ErrorCode.TwoFactorChallengeExpired);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        cookies
            .Should()
            .NotContain(cookie =>
                cookie.Contains("CodigoActivo.Session=", StringComparison.Ordinal)
            );

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.LoginChallenge.Id.Should().BeNull("locking closes the open challenge");
        stored.LastLoginAt.Should().BeNull();
        (await MemberSessionsAsync()).Should().Be(0);

        using var me = await pending.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        await me.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);
    }

    [Fact]
    public async Task LoginCorrectPasswordBeforeTheLimitClearsTheCountedFailures()
    {
        var client = CreateClient();
        for (var attempt = 0; attempt < Threshold - 1; attempt++)
        {
            using var response = await AttemptAsync(client, WrongPassword);
            await response.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
        }

        using var accepted = await AttemptAsync(client, TestSeedData.Password);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.PasswordLockout.FailedAttempts.Should().Be(0);
        MemberLocked(stored).Should().BeFalse();
    }

    [Fact]
    public async Task LoginCorrectPasswordRefusedForTheAccountStatusStillPersistsTheClearedCount()
    {
        var client = CreateClient();
        for (var attempt = 0; attempt < Threshold - 1; attempt++)
        {
            using var wrong = await client.PostJsonAsync(
                LoginUrl,
                new LoginRequest(TestSeedData.PendingEmail, WrongPassword),
                Ct
            );
            await wrong.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
        }

        (await FindAsync<User>(TestSeedData.Users.PendingId))!
            .PasswordLockout.FailedAttempts.Should()
            .Be(Threshold - 1);

        using var refused = await client.PostJsonAsync(
            LoginUrl,
            new LoginRequest(TestSeedData.PendingEmail, TestSeedData.Password),
            Ct
        );
        await refused.ShouldBeForbiddenAsync(ErrorCode.UserAccountPendingVerification);

        var stored = await FindAsync<User>(TestSeedData.Users.PendingId);
        stored!.PasswordLockout.FailedAttempts.Should().Be(0);
        MemberLocked(stored).Should().BeFalse();
    }

    [Fact]
    public async Task ResetPasswordLiftsTheLockAtOnce()
    {
        var client = CreateClient();
        await LockMemberAsync(client);

        var code = await RequestResetCodeAsync(client);
        using var reset = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(code, NewPassword),
            Ct
        );
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        MemberLocked(stored!).Should().BeFalse();
        stored!.PasswordLockout.FailedAttempts.Should().Be(0);

        using var signedIn = await AttemptAsync(client, NewPassword);
        signedIn.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetTwoFactorByAnAdministratorDoesNotUnlockTheAccount()
    {
        var admin = await LoginAsAdminAsync();
        await LockMemberAsync(CreateClient());

        using var response = await admin.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/two-factor/reset",
            new ResetTwoFactorRequest(TestSeedData.Password),
            Ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        MemberLocked((await FindAsync<User>(TestSeedData.Users.MemberId))!).Should().BeTrue();
        using var login = await AttemptAsync(CreateClient(), TestSeedData.Password);
        await login.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
    }

    [Fact]
    public async Task ChangePasswordWrongCurrentPasswordCountsTowardsTheSameLock()
    {
        var client = await LoginAsMemberAsync();

        for (var attempt = 1; attempt <= Threshold; attempt++)
        {
            using var response = await client.PatchJsonAsync(
                $"/api/users/{TestSeedData.Users.MemberId}/password",
                new ChangePasswordRequest(WrongPassword, NewPassword),
                Ct
            );
            await response.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);

            var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
            stored!.PasswordLockout.FailedAttempts.Should().Be(attempt < Threshold ? attempt : 0);
            MemberLocked(stored).Should().Be(attempt >= Threshold);
        }

        using var kept = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        kept.StatusCode.Should().Be(HttpStatusCode.OK);

        using var correctDuringLock = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/password",
            new ChangePasswordRequest(TestSeedData.Password, NewPassword),
            Ct
        );
        await correctDuringLock.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);

        using var login = await AttemptAsync(CreateClient(), TestSeedData.Password);
        await login.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
    }

    [Fact]
    public async Task DeletionCodeWrongPasswordCountsTowardsTheSameLock()
    {
        var client = await LoginAsMemberAsync();

        for (var attempt = 1; attempt <= Threshold; attempt++)
        {
            using var response = await client.PostJsonAsync(
                "/api/me/confirmation-code",
                new ConfirmationCodeRequest(WrongPassword, ConfirmationCodePurpose.AccountDeletion),
                Ct
            );
            await response.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);

            var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
            stored!.PasswordLockout.FailedAttempts.Should().Be(attempt < Threshold ? attempt : 0);
            MemberLocked(stored).Should().Be(attempt >= Threshold);
        }

        using var kept = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        kept.StatusCode.Should().Be(HttpStatusCode.OK);

        var alert = Factory.EmailSender.Sent.Should().ContainSingle().Subject;
        alert.Kind.Should().Be(EmailKind.SecurityAlert);
        alert.ToAddress.Should().Be(TestSeedData.MemberEmail);

        using var login = await AttemptAsync(CreateClient(), TestSeedData.Password);
        await login.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
    }
}
