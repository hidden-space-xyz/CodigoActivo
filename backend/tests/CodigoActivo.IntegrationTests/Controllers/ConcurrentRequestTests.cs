using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.API.Errors;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using ContractGender = CodigoActivo.Application.Abstractions.Querying.ReadModel.Gender;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class ConcurrentRequestTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string NewPassword = "NuevaPass123!";
    private const int Rounds = 6;

    [Fact]
    public async Task ForgotPasswordParallelRequestsForAKnownAddressAnswerAlikeAndEmailOnce()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var statuses = await PostTwiceInParallelAsync(
                "/api/auth/forgot-password",
                new ForgotPasswordRequest(TestSeedData.MemberEmail)
            );

            statuses.Should().AllBeEquivalentTo(HttpStatusCode.NoContent);
            EmailsTo(TestSeedData.MemberEmail, EmailKind.AccessRecovery).Should().Be(1);
        }
    }

    [Fact]
    public async Task ResendVerificationParallelRequestsAnswerAlikeAndEmailOnce()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var statuses = await PostTwiceInParallelAsync(
                "/api/auth/resend-verification",
                new ResendVerificationRequest(TestSeedData.PendingEmail)
            );

            statuses.Should().AllBeEquivalentTo(HttpStatusCode.NoContent);
            EmailsTo(TestSeedData.PendingEmail, EmailKind.AccountVerification).Should().Be(1);
        }
    }

    [Fact]
    public async Task RegisterWhileTheUnverifiedHolderIsWrittenReplacesItAsUsual()
    {
        var codes = new InterleavingCodeHasher();
        await using var host = HostWith<IOneTimeCodeHasher>(codes);
        codes.RunOnceWhileHashing(() =>
            WritePending(
                host.Services,
                pending => pending.IssueOtp("fake:resent", Now(), TimeSpan.FromMinutes(15))
            )
        );
        using var client = host.CreateClient();

        using var response = await client.PostJsonAsync("/api/auth/register", NewAdult(), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.PendingId)).Should().BeNull();
        var replacement = await Factory.QueryAsync(db =>
            db.Users.SingleAsync(
                u => u.Email == EmailAddress.FromStored(TestSeedData.PendingEmail),
                Ct
            )
        );
        replacement.FirstName.Should().Be("Nadia");
        EmailsTo(TestSeedData.PendingEmail, EmailKind.AccountVerification).Should().Be(1);
    }

    [Fact]
    public async Task RegisterWhileTheUnverifiedHolderVerifiesKeepsItAndAnswersAsUsual()
    {
        var codes = new InterleavingCodeHasher();
        await using var host = HostWith<IOneTimeCodeHasher>(codes);
        codes.RunOnceWhileHashing(() =>
            WritePending(host.Services, pending => pending.Verify(Now()))
        );
        using var client = host.CreateClient();

        using var response = await client.PostJsonAsync("/api/auth/register", NewAdult(), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var holder = await FindAsync<User>(TestSeedData.Users.PendingId);
        holder!.Status.Should().Be(UserStatus.Active);
        holder.FirstName.Should().Be("Pedro");
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetPasswordWhileTheOldPasswordLogsInStillReplacesThePassword()
    {
        var hasher = new InterleavingPasswordHasher();
        await using var host = HostWith<IPasswordHasher>(hasher);
        using var client = host.CreateClient();
        using (
            var forgot = await client.PostJsonAsync(
                "/api/auth/forgot-password",
                new ForgotPasswordRequest(TestSeedData.MemberEmail),
                Ct
            )
        )
        {
            forgot.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var code = Factory.EmailSender.LastOtpSentTo(TestSeedData.MemberEmail);
        using var attacker = host.CreateClient();
        HttpStatusCode? login = null;
        hasher.RunOnceWhileHashing(() => login = PassOldPasswordStep(attacker));

        using var reset = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/reset-password",
            new ResetPasswordRequest(code, NewPassword),
            Ct
        );

        login.Should().Be(HttpStatusCode.OK);
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.PasswordHash.Should().Be(FakePasswordHasher.Prefix + NewPassword);
        stored.LoginChallenge.Id.Should().BeNull("the reset closes the challenge opened meanwhile");
    }

    [Fact]
    public async Task LoginPasswordResetWhileItIsVerifiedAnswersInvalidCredentialsWithoutAChallenge()
    {
        var hasher = new InterleavingPasswordHasher();
        await using var host = HostWith<IPasswordHasher>(hasher);
        hasher.RunOnceWhileVerifying(() => ResetMemberPassword(host.Services));
        using var client = host.CreateClient();

        using var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.MemberEmail, TestSeedData.Password),
            Ct
        );

        await response.ShouldBeUnauthorizedAsync(ErrorCode.InvalidCredentials);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        cookies
            .Should()
            .NotContain(cookie =>
                cookie.Contains("CodigoActivo.TwoFactor=", StringComparison.Ordinal)
            );
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.PasswordHash.Should().Be(FakePasswordHasher.Prefix + NewPassword);
        stored.LoginChallenge.Id.Should().BeNull();
        stored.LoginCode.Hash.Should().BeNull();
        stored.PasswordLockout.FailedAttempts.Should().Be(0);
        EmailsTo(TestSeedData.MemberEmail, EmailKind.TwoFactorCode).Should().Be(0);
    }

    [Fact]
    public async Task UpdateUserTargetChangedWhileThePasswordIsVerifiedAnswersConflict()
    {
        var hasher = new InterleavingPasswordHasher();
        await using var host = HostWith<IPasswordHasher>(hasher);
        using var member = await LoginAsync(host, TestSeedData.MemberCredentials);
        hasher.RunOnceWhileVerifying(() => ChangeMemberType(host.Services));

        using var response = await member.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            new UpdateUserRequest(
                "Marta",
                "Miembro",
                TestSeedData.MemberEmail,
                "+34600000099",
                null,
                TestSeedData.MemberNationalId,
                false,
                ContractGender.Female,
                null,
                TestSeedData.Password
            ),
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.ConcurrentUpdate);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.Phone!.Value.Should().Be("+34600000002");
        stored.UserType.Should().Be(UserType.Sponsor);
    }

    private static RegisterRequest NewAdult()
    {
        return new RegisterRequest(
            "Nadia",
            "Nueva",
            TestSeedData.PendingEmail,
            "+34600000099",
            "Str0ngPass!23",
            "87654321X",
            ContractGender.Female,
            true,
            false,
            null,
            null
        );
    }

    private static HttpStatusCode PassOldPasswordStep(HttpClient client)
    {
        using var response = client
            .PostJsonAsync(
                "/api/auth/login",
                new LoginRequest(TestSeedData.MemberEmail, TestSeedData.Password),
                Ct
            )
            .GetAwaiter()
            .GetResult();
        return response.StatusCode;
    }

    private static void WritePending(IServiceProvider services, Action<User> write)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        write(db.Users.Single(u => u.Id == UserId.From(TestSeedData.Users.PendingId)));
        db.SaveChanges();
    }

    private WebApplicationFactory<Program> HostWith<TService>(TService service)
        where TService : class
    {
        return Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TService>();
                services.AddSingleton(service);
            })
        );
    }

    private async Task<HttpStatusCode[]> PostTwiceInParallelAsync(string url, object body)
    {
        Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddHours(3);
        Factory.EmailSender.Clear();
        using var first = CreateClient();
        using var second = CreateClient();
        var responses = await Task.WhenAll(
            first.PostJsonAsync(url, body, Ct),
            second.PostJsonAsync(url, body, Ct)
        );
        foreach (var response in responses)
        {
            response.Dispose();
        }

        return [.. responses.Select(response => response.StatusCode)];
    }

    private int EmailsTo(string address, EmailKind kind)
    {
        return Factory.EmailSender.Sent.Count(message =>
            message.Kind == kind
            && string.Equals(message.ToAddress, address, StringComparison.OrdinalIgnoreCase)
        );
    }

    private DateTimeOffset Now()
    {
        return Factory.Clock.UtcNow;
    }

    private void ResetMemberPassword(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        db.Users.Single(u => u.Id == UserId.From(TestSeedData.Users.MemberId))
            .ResetPassword(FakePasswordHasher.Prefix + NewPassword, Factory.Clock.UtcNow);
        db.SaveChanges();
    }

    private void ChangeMemberType(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        db.Users.Single(u => u.Id == UserId.From(TestSeedData.Users.MemberId))
            .ChangeType(UserType.Sponsor, Factory.Clock.UtcNow);
        db.SaveChanges();
    }

    private sealed class InterleavingPasswordHasher : IPasswordHasher
    {
        private readonly FakePasswordHasher inner = new();
        private Action? whileHashing;
        private Action? whileVerifying;

        public void RunOnceWhileHashing(Action action)
        {
            whileHashing = action;
        }

        public void RunOnceWhileVerifying(Action action)
        {
            whileVerifying = action;
        }

        public string Hash(string password)
        {
            Interlocked.Exchange(ref whileHashing, null)?.Invoke();
            return inner.Hash(password);
        }

        public bool Verify(string password, string hash)
        {
            Interlocked.Exchange(ref whileVerifying, null)?.Invoke();
            return inner.Verify(password, hash);
        }
    }

    private sealed class InterleavingCodeHasher : IOneTimeCodeHasher
    {
        private const string Prefix = "fake:";
        private Action? whileHashing;

        public void RunOnceWhileHashing(Action action)
        {
            whileHashing = action;
        }

        public string Hash(string code)
        {
            Interlocked.Exchange(ref whileHashing, null)?.Invoke();
            return Prefix + code;
        }

        public bool Verify(string code, string hash)
        {
            return string.Equals(hash, Prefix + code, StringComparison.Ordinal);
        }
    }
}
