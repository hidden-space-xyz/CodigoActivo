using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
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

    [Fact]
    public async Task LoginParallelWrongPasswordsCountEveryAttemptOnceAndAlertOnlyOnce()
    {
        const int Attempts = Threshold + 3;
        var clients = new List<HttpClient>();
        var prepared = new List<Task<HttpResponseMessage>>();
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var client = CreateClient();
            clients.Add(client);
        }

        var tokens = new List<string>();
        foreach (var client in clients)
        {
            tokens.Add(await client.FetchCsrfTokenAsync(Ct));
        }

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            prepared.Add(SendPreparedAttemptAsync(clients[attempt], tokens[attempt]));
        }

        var responses = await Task.WhenAll(prepared);
        foreach (var response in responses)
        {
            await response.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
            response.Dispose();
        }

        foreach (var client in clients)
        {
            client.Dispose();
        }

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.IsPasswordLocked().Should().BeTrue();
        stored
            .PasswordFailedAttempts.Should()
            .BeGreaterThanOrEqualTo(Threshold, "no attempt may be lost");
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
            progress!.PasswordFailedAttempts.Should().Be(attempt);
            progress.IsPasswordLocked().Should().Be(attempt >= Threshold);
        }

        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.PasswordLockedAt.Should().Be(Factory.Clock.UtcNow);
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
    public async Task LoginLockingTheAccountRevokesItsOpenSessionsAndWarnsItsOwner()
    {
        var signedIn = await LoginAsMemberAsync();
        using var before = await signedIn.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        before.StatusCode.Should().Be(HttpStatusCode.OK);

        await LockMemberAsync(CreateClient());

        using var after = await signedIn.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        await after.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);
        (
            await Factory.QueryAsync(db =>
                db.UserSessions.CountAsync(row => row.UserId == TestSeedData.Users.MemberId, Ct)
            )
        )
            .Should()
            .Be(0);

        var alert = Factory
            .EmailSender.Sent.Should()
            .ContainSingle(message => message.Kind == EmailKind.SecurityAlert)
            .Subject;
        alert.ToAddress.Should().Be(TestSeedData.MemberEmail);
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
        stored!.LoginChallengeId.Should().BeNull("locking closes the open challenge");
        stored.LastLoginAt.Should().BeNull();
        (
            await Factory.QueryAsync(db =>
                db.UserSessions.CountAsync(row => row.UserId == TestSeedData.Users.MemberId, Ct)
            )
        )
            .Should()
            .Be(0);

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
        stored!.PasswordFailedAttempts.Should().Be(0);
        stored.IsPasswordLocked().Should().BeFalse();
    }

    [Fact]
    public async Task LoginCorrectPasswordRefusedForTheAccountStatusStillPersistsTheClearedCount()
    {
        var client = CreateClient();
        for (var attempt = 0; attempt < Threshold - 1; attempt++)
        {
            using var wrong = await client.PostJsonAsync(
                LoginUrl,
                new LoginRequest(TestSeedData.BlockedEmail, WrongPassword),
                Ct
            );
            await wrong.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
        }

        (await FindAsync<User>(TestSeedData.Users.BlockedId))!
            .PasswordFailedAttempts.Should()
            .Be(Threshold - 1);

        using var refused = await client.PostJsonAsync(
            LoginUrl,
            new LoginRequest(TestSeedData.BlockedEmail, TestSeedData.Password),
            Ct
        );
        await refused.ShouldBeForbiddenAsync(ErrorCode.UserAccountBlocked);

        var stored = await FindAsync<User>(TestSeedData.Users.BlockedId);
        stored!.PasswordFailedAttempts.Should().Be(0);
        stored.IsPasswordLocked().Should().BeFalse();
    }

    [Fact]
    public async Task ResetPasswordIsTheOnlyWayBackInForALockedAccount()
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
        stored!.IsPasswordLocked().Should().BeFalse();
        stored.PasswordFailedAttempts.Should().Be(0);

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

        (await FindAsync<User>(TestSeedData.Users.MemberId))!.IsPasswordLocked().Should().BeTrue();
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
            stored!.PasswordFailedAttempts.Should().Be(attempt);
            stored.IsPasswordLocked().Should().Be(attempt >= Threshold);
        }

        using var revoked = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        await revoked.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);

        using var login = await AttemptAsync(CreateClient(), TestSeedData.Password);
        await login.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
    }
}
