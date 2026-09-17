using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth;

public sealed class LoginCodeIssuerTests
{
    private readonly RecordingEmailSender emailSender = new();
    private readonly TestClock clock = new();
    private readonly TwoFactorOptions options = new() { ChallengeLifetime = TimeSpan.FromMinutes(7) };
    private readonly LoginCodeIssuer sut;

    public LoginCodeIssuerTests()
    {
        var accountEmails = new AccountEmails(
            emailSender,
            new AccountVerificationOptions(),
            new PasswordResetOptions(),
            new ApplicationOptions { BaseUrl = "https://app.test" },
            options
        );
        sut = new LoginCodeIssuer(
            new FakePasswordHasher(),
            options,
            accountEmails,
            NullLogger<LoginCodeIssuer>.Instance
        );
    }

    [Fact]
    public async Task IssueAsyncEmailsASixDigitCodeAndStoresOnlyItsHash()
    {
        var user = NewUser(email: "ana@test.com");

        var result = await sut.IssueAsync(user, clock.UtcNow, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("ana@test.com");
        var code = emailSender.LastLoginCode();
        code.Should().MatchRegex("^[0-9]{6}$");
        message.Subject.Should().NotContain(code, "the code must not leak into the subject line");
        message.TextBody.Should().Contain("7 minutos");
        user.LoginCodeHash.Should().Be(FakePasswordHasher.Prefix + code);
        user.LoginCodeExpiresAt.Should().Be(clock.UtcNow.AddMinutes(7));
        user.LoginCodeLastSentAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task IssueAsyncConsecutiveCodesDiffer()
    {
        var user = NewUser();
        var codes = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 5; i++)
        {
            await sut.IssueAsync(user, clock.UtcNow, TestContext.Current.CancellationToken);
            codes.Add(emailSender.LastLoginCode());
        }

        codes.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task IssueAsyncUserWithoutEmailReturnsConflictWithoutEmailing()
    {
        var user = NewUser(email: null);

        var result = await sut.IssueAsync(user, clock.UtcNow, TestContext.Current.CancellationToken);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserContactInfoRequired);
        emailSender.Sent.Should().BeEmpty();
        user.LoginCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsyncDeliveryFailureReturnsConflictWithoutStagingACode()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = NewUser();

        var result = await sut.IssueAsync(user, clock.UtcNow, TestContext.Current.CancellationToken);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.EmailSendFailed);
        user.LoginCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task IssueAccountDeletionAsyncEmailsTheDeletionCodeAndStoresOnlyItsHash()
    {
        var user = NewUser(email: "ana@test.com");

        var result = await sut.IssueAccountDeletionAsync(
            user,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.TwoFactorCode);
        message.ToAddress.Should().Be("ana@test.com");
        message.Subject.Should().Contain("eliminación");
        var code = emailSender.LastLoginCode();
        code.Should().MatchRegex("^[0-9]{6}$");
        message.Subject.Should().NotContain(code, "the code must not leak into the subject line");
        message.TextBody.Should().Contain("7 minutos");
        user.LoginCodeHash.Should().Be(FakePasswordHasher.Prefix + code);
        user.LoginCodeExpiresAt.Should().Be(clock.UtcNow.AddMinutes(7));
        user.LoginCodeLastSentAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task IssueAccountDeletionAsyncUserWithoutEmailReturnsConflictWithoutEmailing()
    {
        var user = NewUser(email: null);

        var result = await sut.IssueAccountDeletionAsync(
            user,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserContactInfoRequired);
        emailSender.Sent.Should().BeEmpty();
        user.LoginCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task IssueAccountDeletionAsyncQuotaDeniedReturnsConflictWithoutStagingACode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);
        var user = NewUser();

        var result = await sut.IssueAccountDeletionAsync(
            user,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.TwoFactorResendCooldownActive);
        user.LoginCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task IssueAccountDeletionAsyncDeliveryFailureReturnsConflictWithoutStagingACode()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = NewUser();

        var result = await sut.IssueAccountDeletionAsync(
            user,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.EmailSendFailed);
        user.LoginCodeHash.Should().BeNull();
    }
}
